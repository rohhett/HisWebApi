using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Services.Interfaces;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabController : ControllerBase
    {
        private readonly ILabRepository _labRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public LabController(
            ILabRepository repository,
            IResponseMessageService messageService
            )
        {
            _labRepository = repository;
            _messageService = messageService;
        }

      
        [HttpPost("createUpdateSampleTypeMaster")]
        [Authorize]
        public IActionResult CreateUpdateSampleTypeMaster([FromBody] CreateUpdateSampleTypeMasterRequest request)
        {
            _log.Info($"CreateUpdateSampleTypeMaster called. SampleTypeId={request.SampleTypeId}, SampleType={request.SampleType}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for sample type insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsActive value
            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateSampleTypeMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Sample type operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Sample type operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       
        [HttpGet("getAllSampleTypeMaster")]
        [Authorize]
        public IActionResult GetAllSampleTypeMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetAllSampleTypeMaster called. IsActive={isActive?.ToString() ?? "All"}");

            // Validate IsActive parameter if provided
            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                _log.Warn($"Invalid IsActive parameter: {isActive.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 (Inactive), 1 (Active), or null (All)",
                    errors = new { isActive }
                });
            }

            var serviceResult = _labRepository.GetAllSampleTypeMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Sample types fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No sample types found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpGet("getSampleContainerColorMaster")]
        [Authorize]
        public IActionResult GetSampleContainerColorMaster()
        {
            _log.Info("GetSampleContainerColorMaster called.");

            var serviceResult = _labRepository.GetSampleContainerColorMaster();

            if (serviceResult.Result)
                _log.Info($"Container colors fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No container colors found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateLabMethodMaster")]
        [Authorize]
        public IActionResult CreateUpdateLabMethodMaster([FromBody] CreateUpdateLabMethodMasterRequest request)
        {
            _log.Info($"CreateUpdateLabMethodMaster called. MethodId={request.MethodId}, Method={request.Method}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for lab method insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsActive value
            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateLabMethodMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Lab method operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Lab method operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getLabMethodMaster")]
        [Authorize]
        public IActionResult GetLabMethodMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetLabMethodMaster called. IsActive={isActive?.ToString() ?? "All"}");

            // Validate IsActive parameter if provided
            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                _log.Warn($"Invalid IsActive parameter: {isActive.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 (Inactive), 1 (Active), or null (All)",
                    errors = new { isActive }
                });
            }

            var serviceResult = _labRepository.GetLabMethodMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Lab methods fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No lab methods found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPost("createUpdateSampleRemarksMaster")]
        [Authorize]
        public IActionResult CreateUpdateSampleRemarksMaster([FromBody] CreateUpdateSampleRemarksMasterRequest request)
        {
            _log.Info($"CreateUpdateSampleRemarksMaster called. SampleRemarksID={request.SampleRemarksID}, SampleRemarks={request.SampleRemarks}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for sample remarks insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateSampleRemarksMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Sample remarks operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Sample remarks operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSampleRemarksMaster")]
        [Authorize]
        public IActionResult GetSampleRemarksMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetSampleRemarksMaster called. IsActive={isActive?.ToString() ?? "All"}");

            var serviceResult = _labRepository.GetSampleRemarksMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Sample remarks fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No sample remarks found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

     

        [HttpPost("createUpdateSampleRejectionRemarksMaster")]
        [Authorize]
        public IActionResult CreateUpdateSampleRejectionRemarksMaster([FromBody] CreateUpdateSampleRejectionRemarksMasterRequest request)
        {
            _log.Info($"CreateUpdateSampleRejectionRemarksMaster called. SampleRejectionRemarksID={request.SampleRejectionRemarksID}, SampleRejectionRemarks={request.SampleRejectionRemarks}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for sample rejection remarks insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateSampleRejectionRemarksMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Sample rejection remarks operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Sample rejection remarks operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSampleRejectionRemarksMaster")]
        [Authorize]
        public IActionResult GetSampleRejectionRemarksMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetSampleRejectionRemarksMaster called. IsActive={isActive?.ToString() ?? "All"}");

            var serviceResult = _labRepository.GetSampleRejectionRemarksMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Sample rejection remarks fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No sample rejection remarks found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

   

        [HttpPost("createUpdateFieldBoyMaster")]
        [Authorize]
        public IActionResult CreateUpdateFieldBoyMaster([FromBody] CreateUpdateFieldBoyMasterRequest request)
        {
            _log.Info($"CreateUpdateFieldBoyMaster called. FieldBoyId={request.FieldBoyId}, FieldBoyName={request.FieldBoyName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for field boy insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateFieldBoyMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Field boy operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Field boy operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getFieldBoyMaster")]
        [Authorize]
        public IActionResult GetFieldBoyMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetFieldBoyMaster called. IsActive={isActive?.ToString() ?? "All"}");

            var serviceResult = _labRepository.GetFieldBoyMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Field boys fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No field boys found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateInvestigationServiceItemMaster")]
        [Authorize]
        public IActionResult CreateUpdateInvestigationServiceItemMaster([FromBody] CreateUpdateServiceItemRequest request)
        {
            _log.Info($"CreateUpdateInvestigationServiceItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ServiceItem insert/update.");
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
            var serviceResult = _labRepository.CreateUpdateInvestigationServiceItemMaster(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getInvestigationServiceItemList")]
        [Authorize]
        public IActionResult GetInvestigationServiceItemList(

[FromQuery] string categoryTypeId = null,
[FromQuery] string categoryId = null,
    [FromQuery] int? subCategoryId = null,
    [FromQuery] int? subSubCategoryId = null,
    [FromQuery] int? serviceItemId = null,
     [FromQuery] int? labTypeId = null,
   [FromQuery] int? reportTypeId = null,
    [FromQuery] string serviceName = null,
    [FromQuery] int? isActive = null)
        {
            _log.Info($"GetInvestigationServiceItemList called. Id={serviceItemId}, IsActive={isActive}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, ServiceName={serviceName}");

            //if (!categoryId.HasValue || categoryId <= 0)
            //{
            //    var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
            //    return BadRequest(new
            //    {
            //        result = false,
            //        messageType = v.Type,
            //        message = "categoryId is required and must be greater than 0"
            //    });
            //}

            var serviceResult = _labRepository.GetInvestigationServiceItemList(
                serviceItemId,
                isActive,
                categoryTypeId,
                categoryId,
                subCategoryId,
                subSubCategoryId,
                  labTypeId,
                 reportTypeId,
                serviceName
            );

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getObservationMaster")]
        [Authorize]
        public IActionResult GetObservationMaster(
          [FromQuery] int? observationId = null,
          [FromQuery] int? isActive = null)
        {
            _log.Info($"GetObservationMaster API called. observationId={observationId}, isActive={isActive}");

            // Validate isActive when supplied
            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                _log.Warn($"Invalid isActive value: {isActive}");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = "isActive must be 0 or 1."
                });
            }

            var serviceResult = _labRepository.GetObservationMaster(observationId, isActive);

            _log.Info(serviceResult.Result
                ? $"GetObservationMaster succeeded: {serviceResult.Message}"
                : $"GetObservationMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpPost("createUpdateObservationMaster")]
        [Authorize]
        public IActionResult CreateUpdateObservationMaster(
            [FromBody] CreateUpdateObservationMasterRequest request)
        {
            _log.Info($"CreateUpdateObservationMaster API called. " +
                      $"ObservationId={request?.ObservationId}, ObservationName={request?.ObservationName}");

            // ── Model validation (catches [Required] / [StringLength] etc.) ───
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ObservationMaster insert/update.");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = validAlert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateObservationMaster(request, globalValues);

            _log.Info(serviceResult.Result
                ? $"CreateUpdateObservationMaster succeeded: {serviceResult.Message}"
                : $"CreateUpdateObservationMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getInvastigationObservationMapping")]
        [Authorize]
        public IActionResult GetInvastigationObservationMapping([FromQuery] int investigationId)
        {
            _log.Info($"GetInvastigationObservationMapping API called. InvastigationId={investigationId}");

            if (investigationId <= 0)
            {
                _log.Warn($"Invalid InvastigationId={investigationId}");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = "InvastigationId must be greater than 0."
                });
            }

            var serviceResult = _labRepository.GetInvastigationObservationMapping(investigationId);

            _log.Info(serviceResult.Result
                ? $"Succeeded: {serviceResult.Message}"
                : $"Failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("submitInvastigationObservationMapping")]
        [Authorize]
        public IActionResult SubmitInvastigationObservationMapping(
            [FromBody] SubmitInvastigationObservationMappingRequest request)
        {
            _log.Info($"SubmitInvastigationObservationMapping API called. " +
                      $"InvastigationId={request?.InvastigationId}, " +
                      $"ObservationCount={request?.Observations?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state.");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = validAlert.Message,
                    errors = ModelState
                });
            }

            if (request.InvastigationId <= 0)
            {
                _log.Warn($"InvastigationId={request.InvastigationId} is not valid.");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = "InvastigationId must be greater than 0."
                });
            }

            if (request.Observations != null && request.Observations.Any())
            {
                var invalidRows = request.Observations
                    .Where(o => o.InvastigationId <= 0 || o.ObservationId <= 0)
                    .ToList();

                if (invalidRows.Any())
                {
                    _log.Warn($"{invalidRows.Count} row(s) have InvastigationId or ObservationId <= 0.");
                    var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = validAlert.Type,
                        message = "Every observation row must have InvastigationId > 0 and ObservationId > 0."
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.SubmitInvastigationObservationMapping(request, globalValues);

            _log.Info(serviceResult.Result
                ? $"Succeeded: {serviceResult.Message}"
                : $"Failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


      
        [HttpGet("getInvastigationObservationRangeMaster")]
        [Authorize]
        public IActionResult GetInvastigationObservationRangeMaster(
            [FromQuery] int observationId,
            [FromQuery] string gender)
        {
            _log.Info($"GetInvastigationObservationRangeMaster called. " +
                      $"ObservationId={observationId}, Gender={gender}");

            if (observationId <= 0)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "ObservationId must be greater than 0."
                });
            }

            if (string.IsNullOrWhiteSpace(gender) ||
                !new[] { "M", "F", "B" }.Contains(gender.ToUpper()))
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "Gender must be M, F, or B."
                });
            }

            var serviceResult = _labRepository
                .GetInvastigationObservationRangeMaster(observationId, gender.ToUpper());

            _log.Info(serviceResult.Result
                ? $"Succeeded: {serviceResult.Message}"
                : $"Failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("submitInvastigationObservationRangeMaster")]
        [Authorize]
        public IActionResult SubmitInvastigationObservationRangeMaster(
            [FromBody] SubmitInvastigationObservationRangeMasterRequest request)
        {
            _log.Info($"SubmitInvastigationObservationRangeMaster called. " +
                      $"ObservationId={request?.ObservationId}, Gender={request?.Gender}, " +
                      $"RangeCount={request?.Ranges?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = v.Message,
                    errors = ModelState
                });
            }

            if (request.ObservationId <= 0)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "ObservationId must be greater than 0."
                });
            }

            // Validate each row: ObservationId > 0, Gender, FromAge, ToAge required
            if (request.Ranges != null && request.Ranges.Any())
            {
                var badRows = request.Ranges
                    .Where(r => r.ObservationId <= 0 ||
                                string.IsNullOrWhiteSpace(r.Gender) ||
                                string.IsNullOrWhiteSpace(r.FromAge) ||
                                string.IsNullOrWhiteSpace(r.ToAge))
                    .ToList();

                if (badRows.Any())
                {
                    var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = v.Type,
                        message = "Each range row requires ObservationId > 0, Gender, FromAge and ToAge."
                    });
                }
            }

            // Normalize gender to uppercase
            request.Gender = request.Gender.ToUpper();
            if (request.Ranges != null)
                request.Ranges.ForEach(r => r.Gender = r.Gender?.ToUpper());

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.SubmitInvastigationObservationRangeMaster(request, globalValues);

            _log.Info(serviceResult.Result
                ? $"Succeeded: {serviceResult.Message}"
                : $"Failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getFormulaMasterByObservationId")]
        [Authorize]
        public IActionResult GetFormulaMasterByObservationId([FromQuery] int observationId)
        {
            _log.Info($"GetFormulaMasterByObservationId called. ObservationId={observationId}");

            if (observationId <= 0)
            {
                _log.Warn("Invalid ObservationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ObservationId must be greater than 0",
                    errors = new { observationId }
                });
            }

            var serviceResult = _labRepository.GetFormulaMasterByObservationId(observationId);

            if (serviceResult.Result)
                _log.Info($"Formula master fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Formula master fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

     
        [HttpGet("getObservationFormulaByInvestigationId")]
        [Authorize]
        public IActionResult GetObservationFormulaByInvestigationId([FromQuery] int investigationId)
        {
            _log.Info($"GetObservationFormulaByInvestigationId called. InvestigationId={investigationId}");

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

            var serviceResult = _labRepository.GetObservationFormulaByInvestigationId(investigationId);

            if (serviceResult.Result)
                _log.Info($"Observation formula fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Observation formula fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpPost("createUpdateLabFormulaMaster")]
        [Authorize]
        public IActionResult CreateUpdateLabFormulaMaster([FromBody] CreateUpdateLabFormulaMasterRequest request)
        {
            _log.Info($"CreateUpdateLabFormulaMaster called. ObservationId={request.observationId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for lab formula master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.formulaComponents != null && request.formulaComponents.Any())
            {
                var duplicateSeqNos = request.formulaComponents
                    .GroupBy(c => c.sequenceNo)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateSeqNos.Any())
                {
                    _log.Warn($"Duplicate SequenceNo values found: {string.Join(", ", duplicateSeqNos)}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Duplicate SequenceNo values are not allowed in formula components",
                        errors = new { duplicateSequenceNos = duplicateSeqNos }
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateLabFormulaMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Lab formula master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Lab formula master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpPatch("deleteLabFormulaByObservationid")]
        [Authorize]
        public IActionResult DeleteLabFormulaByObservationid([FromQuery] int Observationid)
        {
            _log.Info($"DeleteLabFormulaByObservationid called. Observationid={Observationid}");

            if (Observationid <= 0)
            {
                _log.Warn("Invalid Observationid provided for lab formula deletion.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Observationid must be greater than 0",
                    errors = new { Observationid }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.DeleteLabFormulaByObservationid(Observationid, globalValues);

            if (serviceResult.Result)
                _log.Info($"Lab formula deleted successfully: {serviceResult.Message}");
            else
                _log.Warn($"Lab formula deletion failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }

        [HttpGet("searchPatientInvestigationForSampleManagement")]
        [Authorize]
        public IActionResult SearchPatientInvestigationForSampleManagement(
    [FromQuery] int branchId,
    [FromQuery] int typeId,
    [FromQuery] string uhid = null,
    [FromQuery] string ipdNo = null,
    [FromQuery] string labNo = null,
    [FromQuery] string fromDate = null,
    [FromQuery] string toDate = null,
    [FromQuery] string barCode = null,
    [FromQuery] int subCategoryId = 0,
    [FromQuery] int subSubCategoryId = 0,
    [FromQuery] int investigationId = 0,
    [FromQuery] string patientName = null,
    [FromQuery] int roleId = 0,
    [FromQuery] int corporateId = 0,
    [FromQuery] int statusId = 0,
    [FromQuery] int sampleTypeId = 0,
    [FromQuery] int roomTypeId = 0,
    [FromQuery] string isUrgent = null
            )
        {
            _log.Info($"SearchPatientInvestigationForSampleManagement called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.SearchPatientInvestigationForSampleManagement(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId,sampleTypeId, roomTypeId, isUrgent);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("searchPatientInvestigationForSampleDepartmentReceiving")]
        [Authorize]
        public IActionResult SearchPatientInvestigationForSampleDepartmentReceiving(
[FromQuery] int branchId,
[FromQuery] int typeId,
[FromQuery] string uhid = null,
[FromQuery] string ipdNo = null,
[FromQuery] string labNo = null,
[FromQuery] string fromDate = null,
[FromQuery] string toDate = null,
[FromQuery] string barCode = null,
[FromQuery] int subCategoryId = 0,
[FromQuery] int subSubCategoryId = 0,
[FromQuery] int investigationId = 0,
[FromQuery] string patientName = null,
[FromQuery] int roleId = 0,
[FromQuery] int corporateId = 0,
[FromQuery] int statusId = 0,
[FromQuery] int sampleTypeId = 0,
[FromQuery] int roomTypeId = 0,
[FromQuery] string isUrgent = null
       )
        {
            _log.Info($"SearchPatientInvestigationForSampleDepartmentReceiving called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.SearchPatientInvestigationForSampleDepartmentReceiving(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId, sampleTypeId, roomTypeId, isUrgent);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpGet("searchPatientInvestigationForSampleSegregation")]
        [Authorize]
        public IActionResult SearchPatientInvestigationForSampleSegregation(
 [FromQuery] int branchId,
 [FromQuery] int typeId,
 [FromQuery] string uhid = null,
 [FromQuery] string ipdNo = null,
 [FromQuery] string labNo = null,
 [FromQuery] string fromDate = null,
 [FromQuery] string toDate = null,
 [FromQuery] string barCode = null,
 [FromQuery] int subCategoryId = 0,
 [FromQuery] int subSubCategoryId = 0,
 [FromQuery] int investigationId = 0,
 [FromQuery] string patientName = null,
 [FromQuery] int roleId = 0,
 [FromQuery] int corporateId = 0,
 [FromQuery] int statusId = 0,
 [FromQuery] int sampleTypeId = 0,
 [FromQuery] int roomTypeId = 0,
 [FromQuery] string isUrgent = null
         )
        {
            _log.Info($"SearchPatientInvestigationForSampleSegregation called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.SearchPatientInvestigationForSampleSegregation(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId, sampleTypeId, roomTypeId, isUrgent);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("searchPatientInvestigationForSampleProcessingPathology")]
        [Authorize]
        public IActionResult searchPatientInvestigationForSampleProcessingPathology(
  [FromQuery] int branchId,
  [FromQuery] int typeId,
  [FromQuery] string uhid = null,
  [FromQuery] string ipdNo = null,
  [FromQuery] string labNo = null,
  [FromQuery] string fromDate = null,
  [FromQuery] string toDate = null,
  [FromQuery] string barCode = null,
  [FromQuery] int subCategoryId = 0,
  [FromQuery] int subSubCategoryId = 0,
  [FromQuery] int investigationId = 0,
  [FromQuery] string patientName = null,
  [FromQuery] int roleId = 0,
  [FromQuery] int corporateId = 0,
  [FromQuery] int statusId = 0,
  [FromQuery] int canSampleCollect = 0
          )
        {
            _log.Info($"searchPatientInvestigationForSampleProcessingPathology called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.searchPatientInvestigationForSampleProcessingPathology(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId, canSampleCollect);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("searchPatientInvestigationForSampleProcessingRadiology")]
        [Authorize]
        public IActionResult searchPatientInvestigationForSampleProcessingRadiology(
 [FromQuery] int branchId,
 [FromQuery] int typeId,
 [FromQuery] string uhid = null,
 [FromQuery] string ipdNo = null,
 [FromQuery] string labNo = null,
 [FromQuery] string fromDate = null,
 [FromQuery] string toDate = null,
 [FromQuery] string barCode = null,
 [FromQuery] int subCategoryId = 0,
 [FromQuery] int subSubCategoryId = 0,
 [FromQuery] int investigationId = 0,
 [FromQuery] string patientName = null,
 [FromQuery] int roleId = 0,
 [FromQuery] int corporateId = 0,
 [FromQuery] int statusId = 0
         )
        {
            _log.Info($"searchPatientInvestigationForSampleProcessingRadiology called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.searchPatientInvestigationForSampleProcessingRadiology(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateSampleStatus")]
        [Authorize]
        public IActionResult UpdateSampleStatus([FromBody] UpdateSampleStatusRequest request)
        {
            _log.Info($"UpdateSampleStatus called. Sample count={request?.Samples?.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateSampleStatus.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.Samples == null || !request.Samples.Any())
            {
                _log.Warn("No sample data provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one sample is required",
                    errors = new[] { "Samples list cannot be empty" }
                });
            }

          

            var validStatusIds = new[] { 1,2, 3, 6 };
            var invalidStatuses = request.Samples
                .Where(s => !validStatusIds.Contains(s.StatusId))
                .Select(s => new { s.PatientInvestigationId, s.StatusId })
                .ToList();

            if (invalidStatuses.Any())
            {
                _log.Warn($"Invalid StatusId(s) provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "StatusId must be 1 (Sample Collection), 2 (Sample Segregation), 3 (Dept Receiving), or 6 (Dispatch)",
                    errors = new { invalidStatuses }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.UpdateSampleStatus(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"UpdateSampleStatus completed successfully: {serviceResult.Message}");
            else
                _log.Warn($"UpdateSampleStatus failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("rejectSampleStatus")]
        [Authorize]
        public IActionResult RejectSampleStatus([FromBody] RejectSampleStatusRequest request)
        {
            _log.Info($"RejectSampleStatus called. Sample count={request?.Samples?.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for RejectSampleStatus.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.Samples == null || !request.Samples.Any())
            {
                _log.Warn("No sample reject data provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one sample is required",
                    errors = new[] { "Samples list cannot be empty" }
                });
            }

            var validStatusIds = new[] { 1, 2, 3, 4 };
            var invalidStatuses = request.Samples
                .Where(s => !validStatusIds.Contains(s.StatusId))
                .Select(s => new { s.PatientInvestigationId, s.StatusId })
                .ToList();

            if (invalidStatuses.Any())
            {
                _log.Warn("Invalid StatusId(s) provided for RejectSampleStatus.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "StatusId must be 1 (Sample Rejected), 2 (Rejected Sample Accepted), 3 (Hold), or 4 (UnApproved)",
                    errors = new { invalidStatuses }
                });
            }

            var missingReasons = request.Samples
                .Where(s => s.StatusId != 2 && string.IsNullOrWhiteSpace(s.CancellationReason))
                .Select(s => new { s.PatientInvestigationId, s.StatusId })
                .ToList();

            if (missingReasons.Any())
            {
                _log.Warn("CancellationReason missing for reject/hold/unapproved items.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CancellationReason is required for StatusId 1, 3, and 4",
                    errors = new { missingReasons }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.RejectSampleStatus(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"RejectSampleStatus completed successfully: {serviceResult.Message}");
            else
                _log.Warn($"RejectSampleStatus failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateReportApproval")]
        [Authorize]
        public IActionResult UpdateReportApproval([FromBody] UpdateReportApprovalRequest request)
        {
            _log.Info($"UpdateReportApproval called. PatientInvestigationCount={request?.PatientInvestigationIds?.Count}, BranchId={request?.BranchId}, ApprovedByDoctorId={request?.ApprovedByDoctorId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateReportApproval.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.PatientInvestigationIds == null || !request.PatientInvestigationIds.Any())
            {
                _log.Warn("No patient investigation ids provided for UpdateReportApproval.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one PatientInvestigationId is required",
                    errors = new[] { "PatientInvestigationIds list cannot be empty" }
                });
            }

            var invalidIds = request.PatientInvestigationIds.Where(id => id <= 0).ToList();
            if (invalidIds.Any())
            {
                _log.Warn("Invalid PatientInvestigationId(s) provided for UpdateReportApproval.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All PatientInvestigationIds must be greater than 0",
                    errors = new { invalidIds }
                });
            }

            if (request.BranchId <= 0)
            {
                _log.Warn("Invalid BranchId provided for UpdateReportApproval.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { request.BranchId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.UpdateReportApproval(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"UpdateReportApproval completed successfully: {serviceResult.Message}");
            else
                _log.Warn($"UpdateReportApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientInvestigationDetails")]
        [Authorize]
        public IActionResult GetPatientInvestigationDetails(
    [FromQuery] int branchId,
    [FromQuery] string uhid,
    [FromQuery] int labNo,
    [FromQuery] int visitId)
        {
            _log.Info($"GetPatientInvestigationDetails called. BranchId={branchId}, UHID={uhid}, LabNo={labNo}, VisitId={visitId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId }
                });
            }

            if (string.IsNullOrWhiteSpace(uhid))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UHID is required",
                    errors = new { uhid }
                });
            }

            if (labNo <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "LabNo must be greater than 0",
                    errors = new { labNo }
                });
            }

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _labRepository.GetPatientInvestigationDetails(branchId, uhid, labNo, visitId);

            if (serviceResult.Result)
                _log.Info($"Patient investigation details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Patient investigation details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

    

        [HttpPost("createUpdatePatientInvestigationRemark")]
        [Authorize]
        public IActionResult CreateUpdatePatientInvestigationRemark([FromBody] CreateUpdatePatientInvestigationRemarkRequest request)
        {
            _log.Info($"CreateUpdatePatientInvestigationRemark called. Id={request.Id}, PatientInvestigationId={request.PatientInvestigationId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for patient investigation remark insert/update.");
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
            var serviceResult = _labRepository.CreateUpdatePatientInvestigationRemark(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationRemark operation completed: {serviceResult.Message}");
            else
                _log.Warn($"PatientInvestigationRemark operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientInvestigationRemark")]
        [Authorize]
        public IActionResult GetPatientInvestigationRemark([FromQuery] int patientInvestigationId)
        {
            _log.Info($"GetPatientInvestigationRemark called. PatientInvestigationId={patientInvestigationId}");

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.GetPatientInvestigationRemark(patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationRemark fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientInvestigationRemark fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("deletePatientInvestigationRemark")]
        [Authorize]
        public IActionResult DeletePatientInvestigationRemark([FromQuery] int remarkId, [FromQuery] int patientInvestigationId)
        {
            _log.Info($"DeletePatientInvestigationRemark called. RemarkId={remarkId}, PatientInvestigationId={patientInvestigationId}");

            if (remarkId <= 0)
            {
                _log.Warn("Invalid RemarkId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RemarkId must be greater than 0",
                    errors = new { remarkId }
                });
            }

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.DeletePatientInvestigationRemark(remarkId, patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationRemark deleted successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientInvestigationRemark delete failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateInvestigationDocumentNameMaster")]
        [Authorize]
        public IActionResult CreateUpdateInvestigationDocumentNameMaster([FromBody] CreateUpdateInvestigationDocumentNameMasterRequest request)
        {
            _log.Info($"CreateUpdateInvestigationDocumentNameMaster called. DocumentId={request.DocumentId}, DocumentName={request.DocumentName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for investigation document name insert/update.");
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
            var serviceResult = _labRepository.CreateUpdateInvestigationDocumentNameMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"InvestigationDocumentNameMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"InvestigationDocumentNameMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getInvestigationDocumentNameMaster")]
        [Authorize]
        public IActionResult GetInvestigationDocumentNameMaster()
        {
            _log.Info("GetInvestigationDocumentNameMaster called.");

            var serviceResult = _labRepository.GetInvestigationDocumentNameMaster();

            if (serviceResult.Result)
                _log.Info($"InvestigationDocumentNameMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"InvestigationDocumentNameMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("insertPatientInvestigationDocument")]
        [Authorize]
        public IActionResult InsertPatientInvestigationDocument([FromForm] InsertPatientInvestigationDocumentRequest request)
        {
            _log.Info($"InsertPatientInvestigationDocument called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationDocumentNameId={request.InvestigationDocumentNameId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for patient investigation document insert.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate file
            if (request.UploadFile == null || request.UploadFile.Length == 0)
            {
                _log.Warn("No file uploaded.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Document file is required",
                    errors = new { uploadFile = "File cannot be empty" }
                });
            }

            // Upload file using FileUploadHelper
            var fileUploadHelper = HttpContext.RequestServices.GetRequiredService<FileUploadHelper>();
            var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                request.UploadFile,
                "InvestigationDocuments"
            );

            if (!uploadSuccess)
            {
                _log.Error($"File upload failed: {uploadError}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return StatusCode(500, new
                {
                    result = false,
                    messageType = alert.Type,
                    message = $"File upload failed: {uploadError}"
                });
            }

            _log.Info($"File uploaded successfully: {filePath}");

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.InsertPatientInvestigationDocument(request, globalValues, filePath);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationDocument insert completed: {serviceResult.Message}");
            else
            {
                _log.Warn($"PatientInvestigationDocument insert failed: {serviceResult.Message}");

                // If DB insert failed, clean up the uploaded file
                fileUploadHelper.DeleteFile(filePath);
                _log.Info($"Cleaned up uploaded file after DB failure: {filePath}");
            }

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientInvestigationDocumentList")]
        [Authorize]
        public IActionResult GetPatientInvestigationDocumentList([FromQuery] int patientInvestigationId)
        {
            _log.Info($"GetPatientInvestigationDocumentList called. PatientInvestigationId={patientInvestigationId}");

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.GetPatientInvestigationDocumentList(patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationDocumentList fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientInvestigationDocumentList fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("deletePatientInvestigationDocument")]
        [Authorize]
        public IActionResult DeletePatientInvestigationDocument(
            [FromQuery] int patientDocumentId,
            [FromQuery] int patientInvestigationId)
        {
            _log.Info($"DeletePatientInvestigationDocument called. PatientDocumentId={patientDocumentId}, PatientInvestigationId={patientInvestigationId}");

            if (patientDocumentId <= 0)
            {
                _log.Warn("Invalid PatientDocumentId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientDocumentId must be greater than 0",
                    errors = new { patientDocumentId }
                });
            }

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.DeletePatientInvestigationDocument(patientDocumentId, patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"PatientInvestigationDocument deleted successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientInvestigationDocument delete failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientTabularReportForResultEntry")]
        [Authorize]
        public IActionResult GetPatientTabularReportForResultEntry([FromQuery] int patientInvestigationId)
        {
            _log.Info($"GetPatientTabularReportForResultEntry called. PatientInvestigationId={patientInvestigationId}");

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.GetPatientTabularReportForResultEntry(patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"Tabular report fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Tabular report fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       
        [HttpGet("getPatientFreeTextReportForResultEntry")]
        [Authorize]
        public IActionResult GetPatientFreeTextReportForResultEntry([FromQuery] int patientInvestigationId)
        {
            _log.Info($"GetPatientFreeTextReportForResultEntry called. PatientInvestigationId={patientInvestigationId}");

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.GetPatientFreeTextReportForResultEntry(patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"Free text report fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Free text report fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getAllInvestigationNameOfPatient")]
        [Authorize]
        public IActionResult GetAllInvestigationNameOfPatient(
[FromQuery] int branchId,
[FromQuery] string uhid,
[FromQuery] int labNo,
[FromQuery] int labTypeId,
[FromQuery] int visitId)
        {
            _log.Info($"GetAllInvestigationNameOfPatient called. BranchId={branchId}, UHID={uhid}, LabNo={labNo}, LabTypeId={labTypeId}, VisitId={visitId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId }
                });
            }

            if (string.IsNullOrWhiteSpace(uhid))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UHID is required",
                    errors = new { uhid }
                });
            }

            if (labNo <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "LabNo must be greater than 0",
                    errors = new { labNo }
                });
            }

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            if (labTypeId < 1 || labTypeId > 3)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "LabTypeId must be 1 for Pathology, 2 for Radiology, or 3 for Cardiology",
                    errors = new { labTypeId }
                });
            }

            var serviceResult = _labRepository.GetAllInvestigationNameOfPatient(branchId, uhid, labNo, labTypeId, visitId);

            if (serviceResult.Result)
                _log.Info($"All investigation names fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"All investigation names fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("savePatientTabularReport")]
        [Authorize]
        public IActionResult SavePatientTabularReport([FromBody] SavePatientTabularReportRequest request)
        {
            _log.Info($"SavePatientTabularReport called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationId={request.InvestigationId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for save patient tabular report.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate that TabularReport list is not empty
            if (request.TabularReport == null || !request.TabularReport.Any())
            {
                _log.Warn("TabularReport list is empty.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TabularReport list cannot be empty",
                    errors = new { tabularReport = "At least one observation result is required" }
                });
            }

            // Validate each observation entry
            var invalidObservations = request.TabularReport.Where(r => r.ObservationId <= 0).ToList();
            if (invalidObservations.Any())
            {
                _log.Warn($"Invalid ObservationId(s) found in TabularReport.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All ObservationId values must be greater than 0",
                    errors = new { invalidObservationIds = invalidObservations.Select(r => r.ObservationId).ToList() }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.SavePatientTabularReport(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Tabular report saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Tabular report save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

     
        [HttpPost("savePatientFreeTextReport")]
        [Authorize]
        public IActionResult SavePatientFreeTextReport([FromBody] SavePatientFreeTextReportRequest request)
        {
            _log.Info($"SavePatientFreeTextReport called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationId={request.InvestigationId}, TemplateId={request.TemplateId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for save patient free text report.");
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
            var serviceResult = _labRepository.SavePatientFreeTextReport(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Free text report saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Free text report save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateInvastigationTemplateCommentMaster")]
        [Authorize]
        public IActionResult CreateUpdateInvastigationTemplateCommentMaster([FromBody] List<InvastigationTemplateCommentMasterRequest> request)
        {
            _log.Info($"CreateUpdateInvastigationTemplateCommentMaster called. Count={request?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            if (request == null || !request.Any())
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "At least one item is required", errors = new { request } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateInvastigationTemplateCommentMaster(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       

        [HttpGet("getInvastigationTemplateCommentMaster")]
        [Authorize]
        public IActionResult GetInvastigationTemplateCommentMaster([FromQuery] int id, [FromQuery] int typeId)
        {
            _log.Info($"GetInvastigationTemplateCommentMaster called. Id={id}, TypeId={typeId}");

            if (id <= 0 || typeId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "Id and TypeId must be greater than 0", errors = new { id, typeId } });
            }

            var serviceResult = _labRepository.GetInvastigationTemplateCommentMaster(id, typeId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAllInvestigationTemplateComments")]
        [Authorize]
        public IActionResult GetAllInvestigationTemplateComments(
      [FromQuery] int? isActive = null,
      [FromQuery] int? typeId = null)
        {
            _log.Info($"GetAllInvestigationTemplateComments called. IsActive={isActive?.ToString() ?? "All"}, TypeId={typeId?.ToString() ?? "All"}");

            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 (Inactive), 1 (Active), or null (All)",
                    errors = new { isActive }
                });
            }

            if (isActive.HasValue && (typeId < 1 || typeId > 3))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "typeId must be 1 for Template, 2 for Interpretation, or 3 for Comment",
                    errors = new { typeId }
                });
            }

            var serviceResult = _labRepository.GetAllInvestigationTemplateComments(isActive, typeId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateObservationLOVMaster")]
        [Authorize]
        public IActionResult CreateUpdateObservationLOVMaster([FromBody] CreateUpdateObservationLOVMasterRequest request)
        {
            _log.Info($"CreateUpdateObservationLOVMaster called. LOVId={request.LOVId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateObservationLOVMaster(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getObservationListOfValuesMaster")]
        [Authorize]
        public IActionResult GetObservationListOfValuesMaster()
        {
            _log.Info("GetObservationListOfValuesMaster called.");

            var serviceResult = _labRepository.GetObservationListOfValuesMaster();
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveInvestigationTemplateInterpretationMappings")]
        [Authorize]
        public IActionResult SaveInvestigationTemplateInterpretationMappings([FromBody] List<InvestigationTemplateInterpretationMappingRequest> request)
        {
            _log.Info($"SaveInvestigationTemplateInterpretationMappings called. Count={request?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            if (request == null || !request.Any())
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "At least one mapping item is required", errors = new { request } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.SaveInvestigationTemplateInterpretationMappings(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getInvestigationTemplateInterpretationMappings")]
        [Authorize]
        public IActionResult GetInvestigationTemplateInterpretationMappings([FromQuery] int investigationId)
        {
            _log.Info($"GetInvestigationTemplateInterpretationMappings called. InvestigationId={investigationId}");

            if (investigationId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "InvestigationId must be greater than 0", errors = new { investigationId } });
            }

            var serviceResult = _labRepository.GetInvestigationTemplateInterpretationMappings(investigationId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveObservationCommentsLOVsMappings")]
        [Authorize]
        public IActionResult SaveObservationCommentsLOVsMappings([FromBody] List<ObservationCommentLOVsMappingRequest> request)
        {
            _log.Info($"SaveObservationCommentsLOVsMappings called. Count={request?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            if (request == null || !request.Any())
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "At least one mapping item is required", errors = new { request } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.SaveObservationCommentsLOVsMappings(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getObservationCommentLOVsMappings")]
        [Authorize]
        public IActionResult GetObservationCommentLOVsMappings([FromQuery] int observationId)
        {
            _log.Info($"GetObservationCommentLOVsMappings called. ObservationId={observationId}");

            if (observationId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "ObservationId must be greater than 0", errors = new { observationId } });
            }

            var serviceResult = _labRepository.GetObservationCommentLOVsMappings(observationId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpGet("searchPatientInvestigationForLaboratoryHelpDesk")]
        [Authorize]
        public IActionResult searchPatientInvestigationForLaboratoryHelpDesk(
[FromQuery] int branchId,
[FromQuery] int typeId,
[FromQuery] string uhid = null,
[FromQuery] string ipdNo = null,
[FromQuery] string labNo = null,
[FromQuery] string fromDate = null,
[FromQuery] string toDate = null,
[FromQuery] string barCode = null,
[FromQuery] int subCategoryId = 0,
[FromQuery] int subSubCategoryId = 0,
[FromQuery] int investigationId = 0,
[FromQuery] string patientName = null,
[FromQuery] int roleId = 0,
[FromQuery] int corporateId = 0,
[FromQuery] int statusId = 0
       )
        {
            _log.Info($"searchPatientInvestigationForLaboratoryHelpDesk called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.searchPatientInvestigationForLaboratoryHelpDesk(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #region Histo Template Master

        [HttpPost("createUpdateHistoTemplateMaster")]
        [Authorize]
        public IActionResult CreateUpdateHistoTemplateMaster([FromBody] CreateUpdateHistoTemplateRequest request)
        {
            _log.Info($"CreateUpdateHistoTemplateMaster called. Id={request.Id}, TypeId={request.TypeId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for histo template insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateHistoTemplateMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"HistoTemplate operation completed: {serviceResult.Message}");
            else
                _log.Warn($"HistoTemplate operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getHistoTemplateMaster")]
        [Authorize]
        public IActionResult GetHistoTemplateMaster([FromQuery] int typeId)
        {
            _log.Info($"GetHistoTemplateMaster called. TypeId={typeId}");

            if (typeId <= 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId }
                });
            }

            var serviceResult = _labRepository.GetHistoTemplateMaster(typeId);

            if (serviceResult.Result)
                _log.Info($"HistoTemplate fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No HistoTemplate found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Specimen Master

        [HttpPost("createUpdateSpecimenMaster")]
        [Authorize]
        public IActionResult CreateUpdateSpecimenMaster([FromBody] CreateUpdateSpecimenMasterRequest request)
        {
            _log.Info($"CreateUpdateSpecimenMaster called. ID={request.ID}, SpecimenName={request.SpecimenName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for specimen master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateSpecimenMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SpecimenMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"SpecimenMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSpecimenMaster")]
        [Authorize]
        public IActionResult GetSpecimenMaster()
        {
            _log.Info("GetSpecimenMaster called.");

            var serviceResult = _labRepository.GetSpecimenMaster();

            if (serviceResult.Result)
                _log.Info($"SpecimenMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No specimens found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Specimen Mapping Master

        [HttpPost("createUpdateSpecimenMappingMaster")]
        [Authorize]
        public IActionResult CreateUpdateSpecimenMappingMaster([FromBody] CreateUpdateSpecimenMappingRequest request)
        {
            _log.Info($"CreateUpdateSpecimenMappingMaster called. SpecimenNameId={request.SpecimenNameId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for specimen mapping insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateSpecimenMappingMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SpecimenMapping operation completed: {serviceResult.Message}");
            else
                _log.Warn($"SpecimenMapping operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSpecimenMappingMaster")]
        [Authorize]
        public IActionResult GetSpecimenMappingMaster([FromQuery] int specimenNameId)
        {
            _log.Info($"GetSpecimenMappingMaster called. SpecimenNameId={specimenNameId}");

            if (specimenNameId <= 0)
            {
                _log.Warn("Invalid SpecimenNameId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "SpecimenNameId must be greater than 0",
                    errors = new { specimenNameId }
                });
            }

            var serviceResult = _labRepository.GetSpecimenMappingMaster(specimenNameId);

            if (serviceResult.Result)
                _log.Info($"SpecimenMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No specimen mapping found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Histo Pending Reason Master

        [HttpPost("createUpdateHistoPendingReasonMaster")]
        [Authorize]
        public IActionResult CreateUpdateHistoPendingReasonMaster([FromBody] CreateUpdateHistoPendingReasonRequest request)
        {
            _log.Info($"CreateUpdateHistoPendingReasonMaster called. ID={request.ID}, PendingReason={request.PendingReason}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for pending reason insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateHistoPendingReasonMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"HistoPendingReason operation completed: {serviceResult.Message}");
            else
                _log.Warn($"HistoPendingReason operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getHistoPendingReasonMaster")]
        [Authorize]
        public IActionResult GetHistoPendingReasonMaster()
        {
            _log.Info("GetHistoPendingReasonMaster called.");

            var serviceResult = _labRepository.GetHistoPendingReasonMaster();

            if (serviceResult.Result)
                _log.Info($"HistoPendingReason fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No pending reasons found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Histo Immuno Antibiotic Master

        [HttpPost("createUpdateHistoImmunoAntibioticMaster")]
        [Authorize]
        public IActionResult CreateUpdateHistoImmunoAntibioticMaster([FromBody] CreateUpdateHistoImmunoAntibioticRequest request)
        {
            _log.Info($"CreateUpdateHistoImmunoAntibioticMaster called. ID={request.ID}, AntibioticName={request.AntibioticName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for antibiotic insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateHistoImmunoAntibioticMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"HistoImmunoAntibiotic operation completed: {serviceResult.Message}");
            else
                _log.Warn($"HistoImmunoAntibiotic operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getHistoImmunoAntibioticMaster")]
        [Authorize]
        public IActionResult GetHistoImmunoAntibioticMaster()
        {
            _log.Info("GetHistoImmunoAntibioticMaster called.");

            var serviceResult = _labRepository.GetHistoImmunoAntibioticMaster();

            if (serviceResult.Result)
                _log.Info($"HistoImmunoAntibiotic fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No antibiotics found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion


        // ─────────────────────────────────────────────────────────────────────
        // ORGANISM GROUP
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateOrganismGroup")]
        [Authorize]
        public IActionResult CreateUpdateOrganismGroup([FromBody] CreateUpdateOrganismGroupRequest request)
        {
            _log.Info($"CreateUpdateOrganismGroup called. OrganismGroupId={request.OrganismGroupId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for organism group insert/update.");
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
            var serviceResult = _labRepository.CreateUpdateOrganismGroup(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"OrganismGroup operation completed: {serviceResult.Message}");
            else
                _log.Warn($"OrganismGroup operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOrganismGroupList")]
        [Authorize]
        public IActionResult GetOrganismGroupList()
        {
            _log.Info("GetOrganismGroupList called.");

            var serviceResult = _labRepository.GetOrganismGroupList();

            if (serviceResult.Result)
                _log.Info($"OrganismGroup fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No organism groups found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // ORGANISM NAME
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateOrganismName")]
        [Authorize]
        public IActionResult CreateUpdateOrganismName([FromBody] CreateUpdateOrganismNameRequest request)
        {
            _log.Info($"CreateUpdateOrganismName called. OrganismNameId={request.OrganismNameId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for organism name insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateOrganismName(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"OrganismName operation completed: {serviceResult.Message}");
            else
                _log.Warn($"OrganismName operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOrganismNameList")]
        [Authorize]
        public IActionResult GetOrganismNameList()
        {
            _log.Info("GetOrganismNameList called.");

            var serviceResult = _labRepository.GetOrganismNameList();

            if (serviceResult.Result)
                _log.Info($"OrganismName fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No organism names found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // ANTIBIOTIC GROUP
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateAntibioticGroup")]
        [Authorize]
        public IActionResult CreateUpdateAntibioticGroup([FromBody] CreateUpdateAntibioticGroupRequest request)
        {
            _log.Info($"CreateUpdateAntibioticGroup called. AntibioticGroupId={request.AntibioticGroupId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for antibiotic group insert/update.");
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
            var serviceResult = _labRepository.CreateUpdateAntibioticGroup(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"AntibioticGroup operation completed: {serviceResult.Message}");
            else
                _log.Warn($"AntibioticGroup operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAntibioticGroupList")]
        [Authorize]
        public IActionResult GetAntibioticGroupList()
        {
            _log.Info("GetAntibioticGroupList called.");

            var serviceResult = _labRepository.GetAntibioticGroupList();

            if (serviceResult.Result)
                _log.Info($"AntibioticGroup fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No antibiotic groups found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // ANTIBIOTIC NAME
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateAntibioticName")]
        [Authorize]
        public IActionResult CreateUpdateAntibioticName([FromBody] CreateUpdateAntibioticNameRequest request)
        {
            _log.Info($"CreateUpdateAntibioticName called. AntibioticNameId={request.AntibioticNameId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for antibiotic name insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateAntibioticName(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"AntibioticName operation completed: {serviceResult.Message}");
            else
                _log.Warn($"AntibioticName operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAntibioticNameList")]
        [Authorize]
        public IActionResult GetAntibioticNameList()
        {
            _log.Info("GetAntibioticNameList called.");

            var serviceResult = _labRepository.GetAntibioticNameList();

            if (serviceResult.Result)
                _log.Info($"AntibioticName fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No antibiotic names found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // MICRO TEMPLATE
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateMicroTemplate")]
        [Authorize]
        public IActionResult CreateUpdateMicroTemplate([FromBody] CreateUpdateMicroTemplateRequest request)
        {
            _log.Info($"CreateUpdateMicroTemplate called. Id={request.Id}, TypeId={request.TypeId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for micro template insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive = request.IsActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _labRepository.CreateUpdateMicroTemplate(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"MicroTemplate operation completed: {serviceResult.Message}");
            else
                _log.Warn($"MicroTemplate operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getMicroTemplateList")]
        [Authorize]
        public IActionResult GetMicroTemplateList([FromQuery] int typeId)
        {
            _log.Info($"GetMicroTemplateList called. TypeId={typeId}");

            if (typeId <= 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId }
                });
            }

            var serviceResult = _labRepository.GetMicroTemplateList(typeId);

            if (serviceResult.Result)
                _log.Info($"MicroTemplate fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No micro templates found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // MICRO MAPPING
        // ─────────────────────────────────────────────────────────────────────

        [HttpPost("createUpdateMicroMapping")]
        [Authorize]
        public IActionResult CreateUpdateMicroMapping([FromBody] CreateUpdateMicroMappingRequest request)
        {
            _log.Info($"CreateUpdateMicroMapping called. OrganismId={request.OrganismId}, Items={request.MicroMappings?.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for micro mapping insert/update.");
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
            var serviceResult = _labRepository.CreateUpdateMicroMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"MicroMapping operation completed: {serviceResult.Message}");
            else
                _log.Warn($"MicroMapping operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getMicroMappingByOrganismId")]
        [Authorize]
        public IActionResult GetMicroMappingByOrganismId([FromQuery] int organismId)
        {
            _log.Info($"GetMicroMappingByOrganismId called. OrganismId={organismId}");

            if (organismId <= 0)
            {
                _log.Warn("Invalid OrganismId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "OrganismId must be greater than 0",
                    errors = new { organismId }
                });
            }

            var serviceResult = _labRepository.GetMicroMappingByOrganismId(organismId);

            if (serviceResult.Result)
                _log.Info($"MicroMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No micro mapping found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("searchPatientInvestigationForSampleProcessingHisto")]
        [Authorize]
        public IActionResult searchPatientInvestigationForSampleProcessingHisto(
[FromQuery] int branchId,
[FromQuery] int typeId,
[FromQuery] string uhid = null,
[FromQuery] string ipdNo = null,
[FromQuery] string labNo = null,
[FromQuery] string fromDate = null,
[FromQuery] string toDate = null,
[FromQuery] string barCode = null,
[FromQuery] int subCategoryId = 0,
[FromQuery] int subSubCategoryId = 0,
[FromQuery] int investigationId = 0,
[FromQuery] string patientName = null,
[FromQuery] int roleId = 0,
[FromQuery] int corporateId = 0,
[FromQuery] int statusId = 0,
[FromQuery] int canSampleCollect = 0
        )
        {
            _log.Info($"searchPatientInvestigationForSampleProcessingHisto called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.searchPatientInvestigationForSampleProcessingHisto(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId, canSampleCollect);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("searchPatientInvestigationForSampleProcessingMicro")]
        [Authorize]
        public IActionResult searchPatientInvestigationForSampleProcessingMicro(
[FromQuery] int branchId,
[FromQuery] int typeId,
[FromQuery] string uhid = null,
[FromQuery] string ipdNo = null,
[FromQuery] string labNo = null,
[FromQuery] string fromDate = null,
[FromQuery] string toDate = null,
[FromQuery] string barCode = null,
[FromQuery] int subCategoryId = 0,
[FromQuery] int subSubCategoryId = 0,
[FromQuery] int investigationId = 0,
[FromQuery] string patientName = null,
[FromQuery] int roleId = 0,
[FromQuery] int corporateId = 0,
[FromQuery] int statusId = 0,
[FromQuery] int canSampleCollect = 0
        )
        {
            _log.Info($"searchPatientInvestigationForSampleProcessingMicro called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

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


            if (roleId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "RoleId must be greater than 0" });
            }


            var serviceResult = _labRepository.searchPatientInvestigationForSampleProcessingMicro(
                branchId, typeId, uhid, ipdNo, labNo, fromDate, toDate,
                barCode, subCategoryId, subSubCategoryId, investigationId, patientName, roleId, corporateId, statusId, canSampleCollect);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
       
        [HttpGet("getSampleJourneyByPatientInvestigationId")]
        [Authorize]
        public IActionResult GetSampleJourneyByPatientInvestigationId([FromQuery] int patientInvestigationId)
        {
            _log.Info($"GetSampleJourneyByPatientInvestigationId called. PatientInvestigationId={patientInvestigationId}");

            if (patientInvestigationId <= 0)
            {
                _log.Warn("Invalid PatientInvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientInvestigationId must be greater than 0",
                    errors = new { patientInvestigationId }
                });
            }

            var serviceResult = _labRepository.GetSampleJourneyByPatientInvestigationId(patientInvestigationId);

            if (serviceResult.Result)
                _log.Info($"SampleJourney fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"SampleJourney fetch failed: {serviceResult.Message}");

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
    
