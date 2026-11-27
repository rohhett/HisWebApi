using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Reflection;
using log4net;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.DTO;
using HISWEBAPI.Services;
using HISWEBAPI.Configuration;
using HISWEBAPI.Repositories.Implementations;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorController : ControllerBase
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public DoctorController(
            IDoctorRepository repository,
            IResponseMessageService messageService)
        {
            _doctorRepository = repository;
            _messageService = messageService;
        }

        #region Doctor Department APIs

        /// <summary>
        /// Create or Update Doctor Department
        /// </summary>
        [HttpPost("createUpdateDoctorDepartment")]
        [Authorize]
        public IActionResult CreateUpdateDoctorDepartment([FromBody] CreateUpdateDoctorDepartmentRequest request)
        {
            _log.Info($"CreateUpdateDoctorDepartment called. DepartmentId={request.DepartmentId}, Department={request.Department}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor department insert/update.");
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
            var serviceResult = _doctorRepository.CreateUpdateDoctorDepartment(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor department operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Doctor department operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get Doctor Department List with optional filters
        /// </summary>
        /// <param name="departmentId">Optional department ID filter</param>
        /// <param name="isActive">Optional active status filter (0=Inactive, 1=Active, null=All)</param>
        [HttpGet("getDoctorDepartmentList")]
        [Authorize]
        public IActionResult GetDoctorDepartmentList(
            [FromQuery] int? departmentId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetDoctorDepartmentList called. DepartmentId={departmentId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _doctorRepository.GetDoctorDepartmentList(departmentId, isActive);

            if (serviceResult.Result)
                _log.Info($"Doctor departments fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctor departments found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Doctor Specialization APIs

        /// <summary>
        /// Create or Update Doctor Specialization
        /// </summary>
        [HttpPost("createUpdateDoctorSpecialization")]
        [Authorize]
        public IActionResult CreateUpdateDoctorSpecialization([FromBody] CreateUpdateDoctorSpecializationRequest request)
        {
            _log.Info($"CreateUpdateDoctorSpecialization called. SpecializationId={request.SpecializationId}, Specialization={request.Specialization}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor specialization insert/update.");
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
            var serviceResult = _doctorRepository.CreateUpdateDoctorSpecialization(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor specialization operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Doctor specialization operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get Doctor Specialization List with optional filters
        /// </summary>
        /// <param name="specializationId">Optional specialization ID filter</param>
        /// <param name="isActive">Optional active status filter (0=Inactive, 1=Active, null=All)</param>
        [HttpGet("getDoctorSpecializationList")]
        [Authorize]
        public IActionResult GetDoctorSpecializationList(
            [FromQuery] int? specializationId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetDoctorSpecializationList called. SpecializationId={specializationId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _doctorRepository.GetDoctorSpecializationList(specializationId, isActive);

            if (serviceResult.Result)
                _log.Info($"Doctor specializations fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctor specializations found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion


        /// <summary>Doctor Master Excel upload: SaveData=false validates the rows, SaveData=true saves them (all or nothing).</summary>
        [HttpPost("doctorExcelUpload")]
        [Authorize]
        [RequestSizeLimit(10_000_000)]
        public IActionResult DoctorExcelUpload([FromBody] DoctorExcelUploadRequest request)
        {
            _log.Info($"DoctorExcelUpload called. Rows={request?.Rows?.Count ?? 0}, SaveData={request?.SaveData}");

            if (!ModelState.IsValid || request?.Rows == null || request.Rows.Count == 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = "No doctor rows to upload", errors = ModelState });
            }

            if (request.Rows.Count > Utilities.ExcelUploadHelper.MaxRows)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = $"A file can have at most {Utilities.ExcelUploadHelper.MaxRows} doctor rows" });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.DoctorExcelUpload(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDoctorMaster")]
        [Authorize]
        public IActionResult CreateUpdateDoctorMaster([FromForm] CreateUpdateDoctorMasterRequest request)
        {
            _log.Info($"CreateUpdateDoctorMaster called. DoctorId={request.DoctorId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation for login credentials
            if (request.IsLogin == 1)
            {
                if (string.IsNullOrWhiteSpace(request.UserName))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "UserName is required when IsLogin = 1",
                        errors = new { userName = "Required" }
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Password is required when IsLogin = 1",
                        errors = new { password = "Required" }
                    });
                }
            }


          

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.CreateUpdateDoctorMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Doctor operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPatch("updateDoctorMasterStatus")]
        [Authorize]
        public IActionResult UpdateDoctorMasterStatus([FromQuery] int doctorId, [FromQuery] int isActive)
        {
            _log.Info($"UpdateDoctorMasterStatus called. DoctorId={doctorId}, IsActive={isActive}");

            if (doctorId <= 0)
            {
                _log.Warn("Invalid DoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DoctorId must be greater than 0",
                    errors = new { doctorId }
                });
            }

            if (isActive != 0 && isActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.UpdateDoctorMasterStatus(doctorId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDoctorMaster")]
        [Authorize]
        public IActionResult GetDoctorMaster(
            [FromQuery] int? doctorId = null,
            [FromQuery] int? isDoctorUnit = null,
            [FromQuery] int? doctorDepartmentId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetDoctorMaster called. DoctorId={doctorId?.ToString() ?? "All"}, IsDoctorUnit={isDoctorUnit?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

            // Validate DoctorId if provided
            if (doctorId.HasValue && doctorId.Value <= 0)
            {
                _log.Warn("Invalid DoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DoctorId must be greater than 0",
                    errors = new { doctorId }
                });
            }

            if (doctorDepartmentId.HasValue && doctorDepartmentId.Value <= 0)
            {
                _log.Warn("Invalid doctorDepartmentId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "doctorDepartmentId must be greater than 0",
                    errors = new { doctorId }
                });
            }

            // Validate IsDoctorUnit if provided (must be 0 or 1)
            if (isDoctorUnit.HasValue && isDoctorUnit.Value != 0 && isDoctorUnit.Value != 1)
            {
                _log.Warn($"Invalid IsDoctorUnit parameter: {isDoctorUnit.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsDoctorUnit must be 0 or 1",
                    errors = new { isDoctorUnit }
                });
            }


            // Validate IsActive if provided (must be 0 or 1)
            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                _log.Warn($"Invalid IsActive parameter: {isActive.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive }
                });
            }

            var serviceResult = _doctorRepository.GetDoctorMaster(doctorId, isDoctorUnit, doctorDepartmentId, isActive);

            if (serviceResult.Result)
                _log.Info($"Doctors fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctors found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDoctorUnitMaster")]
        [Authorize]
        public IActionResult createUpdateDoctorUnitMaster([FromBody] CreateUpdateDoctorUnitMasterRequest request)
        {
            _log.Info($"CreateUpdateDoctorUnitMapping called");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor unit mapping insert/update.");
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
            var serviceResult = _doctorRepository.CreateUpdateDoctorUnitMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor unit mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor unit mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDoctorUnitMapping")]
        [Authorize]
        public IActionResult CreateUpdateDoctorUnitMapping([FromBody] CreateUpdateDoctorUnitMappingRequest request)
        {
            _log.Info($"CreateUpdateDoctorUnitMapping called");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor unit mapping insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.UnitId <= 0)
            {
                _log.Warn("Invalid UnitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UnitId must be greater than 0",
                    errors = new { request.UnitId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.CreateUpdateDoctorUnitMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor unit mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor unit mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      

        [HttpGet("getDoctorUnitMapping")]
        [Authorize]
        public IActionResult GetDoctorUnitMapping([FromQuery] int unitId)
        {
            _log.Info($"GetDoctorUnitMapping called. UnitId={unitId}");

            if (unitId <= 0)
            {
                _log.Warn("Invalid UnitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UnitId must be greater than 0",
                    errors = new { unitId }
                });
            }

            var serviceResult = _doctorRepository.GetDoctorUnitMapping(unitId);

            if (serviceResult.Result)
                _log.Info($"Doctors fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctors found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDoctorTimingDetails")]
        [Authorize]
        public IActionResult CreateUpdateDoctorTimingDetails([FromBody] CreateUpdateDoctorTimingDetailsRequest request)
        {
            _log.Info($"CreateUpdateDoctorTimingDetails called. DoctorId={request.DoctorId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor timing details insert/update.");
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
            var serviceResult = _doctorRepository.CreateUpdateDoctorTimingDetails(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor timing details saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor timing details save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getDoctorTimingDetails")]
        [Authorize]
        public IActionResult GetDoctorTimingDetails([FromQuery] int doctorId)
        {
            _log.Info($"GetDoctorTimingDetails called. DoctorId={doctorId}");

            if (doctorId <= 0)
            {
                _log.Warn("Invalid DoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DoctorId must be greater than 0",
                    errors = new { doctorId }
                });
            }

            var serviceResult = _doctorRepository.GetDoctorTimingDetails(doctorId);

            if (serviceResult.Result)
                _log.Info($"Doctor timing details fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctor timing details found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateProMaster")]
        [Authorize]
        public IActionResult CreateUpdateProMaster([FromBody] CreateUpdateProMasterRequest request)
        {
            _log.Info($"CreateUpdateProMaster called. ProId={request.ProId}, ProName={request.ProName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for PRO Master insert/update.");
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
            var serviceResult = _doctorRepository.CreateUpdateProMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"PRO Master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"PRO Master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpGet("getProList")]
        [Authorize]
        public IActionResult GetProList(
            [FromQuery] int? proId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetProList called. ProId={proId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _doctorRepository.GetProList(proId, isActive);

            if (serviceResult.Result)
                _log.Info($"PRO list fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No PRO records found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



      
        [HttpPost("createUpdateReferDoctor")]
        [Authorize]
        public IActionResult CreateUpdateReferDoctor([FromBody] CreateUpdateReferDoctorRequest request)
        {
            _log.Info($"CreateUpdateReferDoctor called. ReferDoctorId={request.ReferDoctorId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for Refer Doctor insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate Active value
            if (request.Active != 0 && request.Active != 1)
            {
                _log.Warn("Invalid Active value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Active must be 0 or 1",
                    errors = new { active = request.Active }
                });
            }

            // Validate ProId
            if (request.ProId < 0)
            {
                _log.Warn("Invalid ProId value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ProId must be greater than or equal to 0",
                    errors = new { proId = request.ProId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.CreateUpdateReferDoctor(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Refer Doctor operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Refer Doctor operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateReferDoctorMasterStatus")]
        [Authorize]
        public IActionResult UpdateReferDoctorMasterStatus([FromQuery] int referDoctorId, [FromQuery] int isActive)
        {
            _log.Info($"UpdateReferDoctorMasterStatus called. ReferDoctorId={referDoctorId}, IsActive={isActive}");

            if (referDoctorId <= 0)
            {
                _log.Warn("Invalid ReferDoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ReferDoctorId must be greater than 0",
                    errors = new { referDoctorId }
                });
            }

            if (isActive != 0 && isActive != 1)
            {
                _log.Warn("Invalid IsActive value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1",
                    errors = new { isActive }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _doctorRepository.UpdateReferDoctorMasterStatus(referDoctorId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"ReferDoctor status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"ReferDoctor status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getReferDoctorList")]
        [Authorize]
        public IActionResult GetReferDoctorList(
            [FromQuery] int? referDoctorId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetReferDoctorList called. ReferDoctorId={referDoctorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _doctorRepository.GetReferDoctorList(referDoctorId, isActive);

            if (serviceResult.Result)
                _log.Info($"Refer Doctor list fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No Refer Doctor records found: {serviceResult.Message}");

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