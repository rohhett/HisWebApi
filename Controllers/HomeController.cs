using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Services.Implementations;
using HISWEBAPI.Services.Interfaces;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly IHomeRepository _homeRepository;
        private readonly IResponseMessageService _messageService;
        private readonly IPatientInvestigationReportPdfService _patientInvestigationReportPdfService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public HomeController(
            IHomeRepository repository,
        IResponseMessageService messageService,
        IPatientInvestigationReportPdfService patientInvestigationReportPdfService)
        {
            _homeRepository = repository;
            _messageService = messageService;
            _patientInvestigationReportPdfService = patientInvestigationReportPdfService;

        }


        [HttpPost("clearAllCache")]
        [Authorize]
        public IActionResult ClearAllCache()
        {
            _log.Info("ClearAllCache API endpoint called.");


            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _homeRepository.ClearAllCache();

            if (serviceResult.Result)
            {
                _log.Info($"Cache cleared successfully by UserId={globalValues.userId} from IP={globalValues.ipAddress}");
            }
            else
            {
                _log.Warn($"Cache clearing failed: {serviceResult.Message}");
            }

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });

        }

        [HttpGet("getMobileAppSettings")]
        [AllowAnonymous]
        public IActionResult GetMobileAppSettings()
        {
            _log.Info("GetMobileAppSettings called.");

            var serviceResult = _homeRepository.GetMobileAppSettings();

            if (serviceResult.Result)
                _log.Info($"Mobile app settings fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Mobile app settings fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getActiveBranchList")]
        [AllowAnonymous]
        public IActionResult GetActiveBranchList()
        {
            _log.Info("GetActiveBranchList called.");
           
                var serviceResult = _homeRepository.GetActiveBranchList();

                if (serviceResult.Result)
                    _log.Info($"Branches fetched: {serviceResult.Message}");
                else
                    _log.Warn($"No branches found: {serviceResult.Message}");

                return StatusCode(serviceResult.StatusCode, new
                {
                    result = serviceResult.Result,
                    messageType = serviceResult.MessageType,
                    message = serviceResult.Message,
                    data = serviceResult.Data
                });
        }

        
        [HttpGet("getPickListMaster")]
        [AllowAnonymous]
        public IActionResult GetPickListMaster([FromQuery] string fieldName)
        {
            _log.Info($"GetPickListMaster called with fieldName: {fieldName}");
           
                var serviceResult = _homeRepository.GetPickListMaster(fieldName);

                if (serviceResult.Result)
                    _log.Info($"PickList fetched: {serviceResult.Message}");
                else
                    _log.Warn($"No PickList found: {serviceResult.Message}");

                return StatusCode(serviceResult.StatusCode, new
                {
                    result = serviceResult.Result,
                    messageType = serviceResult.MessageType,
                    message = serviceResult.Message,
                    data = serviceResult.Data
                });
            
        }

        [HttpGet("getPredefineQueryResult")]
        [Authorize]
        public IActionResult GetPredefineQueryResult(
    [FromQuery] string queryName,
    [FromQuery] string filter1 = null,
    [FromQuery] string filter2 = null)
        {
            _log.Info($"GetPredefineQueryResult called. QueryName={queryName}, Filter1={filter1 ?? "null"}, Filter2={filter2 ?? "null"}");

            if (string.IsNullOrWhiteSpace(queryName))
            {
                _log.Warn("QueryName is missing or empty.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "QueryName is required",
                    errors = new { queryName }
                });
            }

            var serviceResult = _homeRepository.GetPredefineQueryResult(queryName, filter1, filter2);

            if (serviceResult.Result)
                _log.Info($"PredefineQueryResult fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PredefineQueryResult fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateResponseMessage")]
        [Authorize]
        public IActionResult CreateUpdateResponseMessage([FromBody] ResponseMessageRequest request)
        {
            _log.Info("CreateUpdateResponseMessage called.");
           
                if (!ModelState.IsValid)
                {
                    _log.Warn("Invalid model state for Response message insert/update.");
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

                var jsonResult = _messageService.CreateUpdateResponseMessage(request, globalValues);
                var result = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonResult);

                if (result.result == false)
                {
                    _log.Warn($"Response message operation failed: {result.message}");
                    return Conflict(new
                    {
                        result = false,
                        messageType = result.messageType?.ToString() ?? "Error",
                        message = result.message.ToString()
                    });
                }

                _log.Info($"Response message operation completed: {result.message}");
                return Ok(new
                {
                    result = true,
                    messageType = result.messageType?.ToString() ?? "Info",
                    message = result.message.ToString()
                });
          
        }

        [HttpGet("getAllGlobalValues")]
        [Authorize]
        public IActionResult GetAllGlobalValues()
        {
            _log.Info("GetAllGlobalValues endpoint called.");

                var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

                _log.Info($"Global values retrieved: HospId={globalValues.hospId}, UserId={globalValues.userId}, IpAddress={globalValues.ipAddress}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");

                return Ok(new
                {
                    result = true,
                    messageType = alert.Type,
                    message = alert.Message,
                    data = globalValues
                });
            
           
        }


        [HttpPost("sendMobileVerificationOtp")]
        [Authorize]
        public IActionResult SendMobileVerificationOtp([FromBody] SendMobileVerificationOtpRequest request)
        {
            _log.Info($"SendMobileVerificationOtp called. MobileNumber={request.MobileNumber}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var serviceResult = _homeRepository.SendMobileVerificationOtp(request);

            if (serviceResult.Result)
                _log.Info($"Mobile verification OTP sent: {serviceResult.Message}");
            else
                _log.Warn($"Mobile verification OTP send failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("verifyMobileVerificationOtp")]
        [Authorize]
        public IActionResult VerifyMobileVerificationOtp([FromBody] VerifyMobileVerificationOtpRequest request)
        {
            _log.Info($"VerifyMobileVerificationOtp called. MobileNumber={request.MobileNumber}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var serviceResult = _homeRepository.VerifyMobileVerificationOtp(request);

            if (serviceResult.Result)
                _log.Info($"Mobile number verified: {serviceResult.Message}");
            else
                _log.Warn($"Mobile OTP verification failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }

        [HttpPost("sendEmailVerificationOtp")]
        [Authorize]
        public IActionResult SendEmailVerificationOtp([FromBody] SendEmailVerificationOtpRequest request)
        {
            _log.Info($"SendEmailVerificationOtp called. Email={request.Email}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var serviceResult = _homeRepository.SendEmailVerificationOtp(request);

            if (serviceResult.Result)
                _log.Info($"Email verification OTP sent: {serviceResult.Message}");
            else
                _log.Warn($"Email verification OTP send failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("verifyEmailVerificationOtp")]
        [Authorize]
        public IActionResult VerifyEmailVerificationOtp([FromBody] VerifyEmailVerificationOtpRequest request)
        {
            _log.Info($"VerifyEmailVerificationOtp called. Email={request.Email}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var serviceResult = _homeRepository.VerifyEmailVerificationOtp(request);

            if (serviceResult.Result)
                _log.Info($"Email verified: {serviceResult.Message}");
            else
                _log.Warn($"Email OTP verification failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }


        [HttpGet("getCountryMaster")]
        [Authorize]
        public IActionResult GetCountryMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetCountryMaster called. IsActive={isActive?.ToString() ?? "All"}");

            var serviceResult = _homeRepository.GetCountryMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Countries fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No countries found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getStateMaster")]
        [Authorize]
        public IActionResult GetStateMaster([FromQuery] int countryId, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetStateMaster called. CountryId={countryId}, IsActive={isActive?.ToString() ?? "All"}");

            if (countryId <= 0)
            {
                _log.Warn("Invalid CountryId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CountryId must be greater than 0",
                    errors = new { countryId }
                });
            }

            var serviceResult = _homeRepository.GetStateMaster(countryId, isActive);

            if (serviceResult.Result)
                _log.Info($"States fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No states found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDistrictMaster")]
        [Authorize]
        public IActionResult GetDistrictMaster([FromQuery] int stateId, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetDistrictMaster called. StateId={stateId}, IsActive={isActive?.ToString() ?? "All"}");

            if (stateId <= 0)
            {
                _log.Warn("Invalid StateId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "StateId must be greater than 0",
                    errors = new { stateId }
                });
            }

            var serviceResult = _homeRepository.GetDistrictMaster(stateId, isActive);

            if (serviceResult.Result)
                _log.Info($"Districts fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No districts found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCityMaster")]
        [Authorize]
        public IActionResult GetCityMaster([FromQuery] int districtId, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetCityMaster called. DistrictId={districtId}, IsActive={isActive?.ToString() ?? "All"}");

            if (districtId <= 0)
            {
                _log.Warn("Invalid DistrictId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DistrictId must be greater than 0",
                    errors = new { districtId }
                });
            }

            var serviceResult = _homeRepository.GetCityMaster(districtId, isActive);

            if (serviceResult.Result)
                _log.Info($"Cities fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No cities found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getPincodeMaster")]
        [Authorize]
        public IActionResult GetPincodeMaster([FromQuery] int cityId, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetPincodeMaster called. CityId={cityId}, IsActive={isActive?.ToString() ?? "All"}");

            if (cityId <= 0)
            {
                _log.Warn("Invalid CityId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CityId must be greater than 0",
                    errors = new { cityId }
                });
            }

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

            var serviceResult = _homeRepository.GetPincodeMaster(cityId, isActive);

            if (serviceResult.Result)
                _log.Info($"Pincodes fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No pincodes found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getLocationByPincode")]
        [Authorize]
        public IActionResult GetLocationByPincode([FromQuery] int pincode)
        {
            _log.Info($"GetLocationByPincode called. Pincode={pincode}");

            // Validate pincode format (6 digits)
            if (pincode < 100000 || pincode > 999999)
            {
                _log.Warn($"Invalid pincode format: {pincode}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Pincode must be exactly 6 digits",
                    errors = new { pincode }
                });
            }

            var serviceResult = _homeRepository.GetLocationByPincode(pincode);

            if (serviceResult.Result)
                _log.Info($"Location fetched successfully for pincode: {pincode}");
            else
                _log.Warn($"Location fetch failed for pincode: {pincode}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }




        [HttpGet("getAllInsuranceCompanyList")]
        [Authorize]
        public IActionResult GetAllInsuranceCompanyList()
        {
            _log.Info("GetAllInsuranceCompanyList API called.");

            var serviceResult = _homeRepository.GetAllInsuranceCompanyList();

            if (serviceResult.Result)
                _log.Info($"Insurance companies fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No insurance companies found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpGet("getCorporateListByInsuranceCompanyId")]
        [Authorize]
        public IActionResult GetCorporateListByInsuranceCompanyId(
            [FromQuery] int? insuranceCompanyId,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetCorporateListByInsuranceCompanyId API called. InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

            if (insuranceCompanyId == null || insuranceCompanyId < 0)
            {
                _log.Warn("Invalid insuranceCompanyId supplied.");
                return BadRequest(new
                {
                    result = false,
                    messageType = "ERROR",
                    message = "insuranceCompanyId is mandatory and must be greater than equal to 0.",
                    data = ""
                });
            }

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

            var serviceResult = _homeRepository.GetCorporateListByInsuranceCompanyId(
                insuranceCompanyId,
                isActive);

            if (serviceResult.Result)
                _log.Info($"Corporates fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No corporates found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateListByBranchIdAndInsuranceCompanyId")]
        [Authorize]
        public IActionResult GetCorporateListByBranchIdAndInsuranceCompanyId(
           [FromQuery] int? branchId,
           [FromQuery] int? insuranceCompanyId
           )
        {
            _log.Info($"GetCorporateListByInsuranceCompanyId API called. InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}");

            if (branchId == null || branchId <= 0)
            {
                _log.Warn("Invalid branchId supplied.");
                return BadRequest(new
                {
                    result = false,
                    messageType = "ERROR",
                    message = "branchId is mandatory and must be greater than 0.",
                    data = ""
                });
            }

            if (insuranceCompanyId == null || insuranceCompanyId < 0)
            {
                _log.Warn("Invalid insuranceCompanyId supplied.");
                return BadRequest(new
                {
                    result = false,
                    messageType = "ERROR",
                    message = "insuranceCompanyId is mandatory and must be greater than equal to 0.",
                    data = ""
                });
            }

            var serviceResult = _homeRepository.GetCorporateListByBranchIdAndInsuranceCompanyId(
                branchId,
                insuranceCompanyId
                );

            if (serviceResult.Result)
                _log.Info($"Corporates fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No corporates found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("uploadDocument")]
        [Authorize]
        public IActionResult UploadDocument([FromForm] UploadDocumentFromFileManagerRequest request)
        {
            _log.Info($"UploadDocument called. FileName={request?.File?.FileName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UploadDocument.");
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
            var serviceResult = _homeRepository.UploadDocument(request, globalValues);

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

        [HttpGet("getFile")]
        [Authorize] 
        public IActionResult GetFile([FromQuery] string filePath)
        {
            _log.Info($"GetFile called. FilePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _log.Warn("File path is null or empty");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "File path is required",
                    errors = new { filePath = "File path cannot be empty" }
                });
            }

            var serviceResult = _homeRepository.GetFile(filePath);

            if (serviceResult.Result)
            {
                _log.Info($"File retrieved successfully: {serviceResult.Message}");
                return File(
                    serviceResult.Data.FileStream,
                    serviceResult.Data.ContentType,
                    serviceResult.Data.FileName
                );
            }
            else
            {
                _log.Warn($"File retrieval failed: {serviceResult.Message}");
                return StatusCode(serviceResult.StatusCode, new
                {
                    result = serviceResult.Result,
                    messageType = serviceResult.MessageType,
                    message = serviceResult.Message
                });
            }
        }

        [HttpGet("getFileAsBase64")]
        [Authorize]
        public IActionResult GetFileAsBase64([FromQuery] string filePath)
        {
            _log.Info($"GetFileAsBase64 called. FilePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                _log.Warn("File path is null or empty");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "File path is required"
                });
            }

            var serviceResult = _homeRepository.GetFileAsBase64(filePath);

            if (serviceResult.Result)
                _log.Info($"File retrieved as base64 successfully: {serviceResult.Message}");
            else
                _log.Warn($"File retrieval as base64 failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("fileExists")]
        [Authorize]
        public IActionResult FileExists([FromQuery] string filePath)
        {
            _log.Info($"FileExists called. FilePath={filePath}");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "File path is required"
                });
            }

            var serviceResult = _homeRepository.CheckFileExists(filePath);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDoctorMasterListByBranchId")]
        [Authorize]
        public IActionResult GetDoctorMasterListByBranchId(
           [FromQuery] int branchId,
           [FromQuery] string departmentId = null,
           [FromQuery] string specializationId = null,
           [FromQuery] int? canApproveLabReport = null,
           [FromQuery] byte? isDoctorUnit = null)
        {
            _log.Info($"GetDoctorMasterListByBranchId called. BranchId={branchId}, DepartmentId={departmentId ?? "All"}, SpecializationId={specializationId ?? "All"}, CanApproveLabReport={canApproveLabReport?.ToString() ?? "All"}, IsDoctorUnit={isDoctorUnit?.ToString() ?? "All"}");

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

            // Validate departmentId format if provided
            if (!string.IsNullOrWhiteSpace(departmentId))
            {
                var parts = departmentId.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => !int.TryParse(p.Trim(), out _)))
                {
                    _log.Warn($"Invalid DepartmentId format: {departmentId}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "DepartmentId must be a comma-separated list of integers (e.g. 1,2,3)",
                        errors = new { departmentId }
                    });
                }
            }

            // Validate specializationId format if provided
            if (!string.IsNullOrWhiteSpace(specializationId))
            {
                var parts = specializationId.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => !int.TryParse(p.Trim(), out _)))
                {
                    _log.Warn($"Invalid SpecializationId format: {specializationId}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "SpecializationId must be a comma-separated list of integers (e.g. 1,2,3)",
                        errors = new { specializationId }
                    });
                }
            }

            var serviceResult = _homeRepository.GetDoctorMasterListByBranchId(
                branchId,
                departmentId,
                specializationId,
                canApproveLabReport,
                isDoctorUnit);

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

        [HttpGet("getCategoryTypeList")]
        [Authorize]
        public IActionResult GetCategoryTypeList([FromQuery] string categoryTypeIds = null)
        {
            _log.Info($"GetCategoryTypeList called. categoryTypeIds={categoryTypeIds}");

            var serviceResult = _homeRepository.GetCategoryTypeList(categoryTypeIds);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCategoryList")]
        [Authorize]
        public IActionResult GetCategoryList(
     [FromQuery] string categoryIds = null,
     [FromQuery] string categoryTypeIds = null)
        {
            _log.Info($"GetCategoryList called. CategoryIds={categoryIds}, CategoryTypeIds={categoryTypeIds}");
            var serviceResult = _homeRepository.GetCategoryList(categoryIds, categoryTypeIds);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateCategory")]
        [Authorize]
        public IActionResult CreateUpdateCategory([FromBody] CreateUpdateCategoryRequest request)
        {
            _log.Info($"CreateUpdateCategory called. CategoryId={request.CategoryId}, CategoryName={request.CategoryName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for category insert/update.");
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
            var serviceResult = _homeRepository.CreateUpdateCategory(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Category operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Category operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSubCategoryList")]
        [Authorize]
        public IActionResult GetSubCategoryList([FromQuery] string categoryIds = null)
        {
            _log.Info($"GetSubCategoryList called. CategoryIds={categoryIds}");

            var serviceResult = _homeRepository.GetSubCategoryList(categoryIds);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateSubCategory")]
        [Authorize]
        public IActionResult CreateUpdateSubCategory([FromBody] CreateUpdateSubCategoryRequest request)
        {
            _log.Info($"CreateUpdateSubCategory called. SubCategoryId={request.SubCategoryId}, Name={request.SubCategoryName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SubCategory insert/update.");
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
            var serviceResult = _homeRepository.CreateUpdateSubCategory(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getSubSubCategoryList")]
        [AllowAnonymous]
        public IActionResult GetSubSubCategoryList([FromQuery] string subCategoryIds = null)
        {
            _log.Info($"GetSubSubCategoryList called. SubCategoryIds={subCategoryIds}");

            var serviceResult = _homeRepository.GetSubSubCategoryList(subCategoryIds);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
      
        [HttpPost("createUpdateSubSubCategory")]
        [Authorize]
        public IActionResult CreateUpdateSubSubCategory([FromBody] CreateUpdateSubSubCategoryRequest request)
        {
            _log.Info($"CreateUpdateSubSubCategory called. SubSubCategoryId={request.SubSubCategoryId}, Name={request.SubSubCategoryName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SubSubCategory insert/update.");
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
            var serviceResult = _homeRepository.CreateUpdateSubSubCategory(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("GetServiceItemList")]
        [Authorize]
        public IActionResult GetServiceItemList(

[FromQuery] string categoryTypeId = null,
[FromQuery] string categoryId = null,
   [FromQuery] int? subCategoryId = null,
   [FromQuery] int? subSubCategoryId = null,
   [FromQuery] int? labTypeId = null,
   [FromQuery] int? reportTypeId = null,
   [FromQuery] int? serviceItemId = null,
   [FromQuery] string serviceName = null,
   [FromQuery] int? isRegistrationCharge = null,
   [FromQuery] int? isActive = null)
        {
            _log.Info($"GetServiceItemList called. Id={serviceItemId}, IsActive={isActive}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, ServiceName={serviceName}");

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

            var serviceResult = _homeRepository.GetServiceItemList(
                serviceItemId,
                isActive,
                categoryTypeId,
                categoryId,
                subCategoryId,
                subSubCategoryId,
                labTypeId,
                reportTypeId,
                serviceName,
                isRegistrationCharge
            );

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpGet("getPaymentModeMasterList")]
        [Authorize]
        public IActionResult GetPaymentModeMasterList(
    [FromQuery] string paymentModeName = null,
    [FromQuery] int? isActive = null)
        {
            _log.Info($"GetPaymentModeMasterList called. PaymentModeName={paymentModeName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _homeRepository.GetPaymentModeMasterList(paymentModeName, isActive);

            if (serviceResult.Result)
                _log.Info($"Payment modes fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No payment modes found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateServiceItemMasterStatus")]
        [Authorize]
        public IActionResult UpdateServiceItemMasterStatus([FromQuery] int serviceItemId, [FromQuery] int isActive)
        {

            if (serviceItemId <= 0)
            {
                _log.Warn("Invalid serviceItemId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "serviceItemId must be greater than 0",
                    errors = new { serviceItemId }
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
            var serviceResult = _homeRepository.UpdateServiceItemMasterStatus(serviceItemId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"service status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"service status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporatePaymentModes")]
        [Authorize]
        public IActionResult GetCorporatePaymentModes(
            [FromQuery] int corporateId,
            [FromQuery] int isRefundPaymentModes = 0)
        {
            _log.Info($"GetCorporatePaymentModes called. CorporateId={corporateId}, IsRefundPaymentModes={isRefundPaymentModes}");

            // corporateId must be >= 0 (0 = general, > 0 = specific corporate)
            if (corporateId < 0)
            {
                _log.Warn("Invalid CorporateId provided.");
                var alertVal = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alertVal.Type,
                    message = "CorporateId must be 0 or greater",
                    errors = new { corporateId }
                });
            }

            if (isRefundPaymentModes != 0 && isRefundPaymentModes != 1)
            {
                _log.Warn("Invalid IsRefundPaymentModes value.");
                var alertVal = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alertVal.Type,
                    message = "IsRefundPaymentModes must be 0 or 1",
                    errors = new { isRefundPaymentModes }
                });
            }

            var serviceResult = _homeRepository.GetCorporatePaymentModes(corporateId, isRefundPaymentModes);

            if (serviceResult.Result)
                _log.Info($"Payment modes fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Payment modes fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDiscountApprovalForBilling")]
        [Authorize]
        public IActionResult GetDiscountApprovalForBilling(
           [FromQuery] int branchId,
           [FromQuery] string discountType ="OPD")
        {

            if (branchId <= 0)
            {
                _log.Warn("Invalid branchId provided.");
                var alertVal = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alertVal.Type,
                    message = "branchId must be greater than 0",
                    errors = new { branchId }
                });
            }

            if (discountType != "OPD" && discountType != "IPD" && discountType != "Store")
            {
                _log.Warn("Invalid discountType provided.");
                var alertVal = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alertVal.Type,
                    message = "Discount Type must be OPD or IPD or Store",
                    errors = new { branchId }
                });
            }



            var serviceResult = _homeRepository.GetDiscountApprovalForBilling(discountType, branchId);

          

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
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

        [HttpGet("checkBedStatus")]
        [Authorize]
        public IActionResult CheckBedStatus([FromQuery] int bedId)
        {
            _log.Info($"CheckBedStatus called. BedId={bedId}");

            if (bedId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BedId must be greater than 0" });
            }

            var serviceResult = _homeRepository.CheckBedStatus(bedId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("checkPatientAdmitted")]
        [Authorize]
        public IActionResult CheckPatientAdmitted([FromQuery] int patientId)
        {
            _log.Info($"CheckPatientAdmitted called. PatientId={patientId}");

            if (patientId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PatientId must be greater than 0" });
            }

            var serviceResult = _homeRepository.CheckPatientAdmitted(patientId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBedTypes")]
        [Authorize]
        public IActionResult GetBedTypes([FromQuery] int branchId, [FromQuery] int roomTypeId)
        {
            _log.Info($"GetBedTypes called. BranchId={branchId}, RoomTypeId={roomTypeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

            if (roomTypeId < 1 || roomTypeId > 4)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoomTypeId must be 1 (Normal), 2 (Day Care), 3 (Dialysis), or 4 (Emergency)"
                });
            }

            var serviceResult = _homeRepository.GetBedTypes(branchId, roomTypeId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAvailableBeds")]
        [Authorize]
        public IActionResult GetAvailableBeds([FromQuery] int branchId, [FromQuery] int typeId)
        {
            _log.Info($"GetAvailableBeds called. BranchId={branchId}, TypeId={typeId}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

            if (typeId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "TypeId must be greater than 0" });
            }

            var serviceResult = _homeRepository.GetAvailableBeds(branchId, typeId);
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillingTabs")]
        [Authorize]
        public IActionResult GetBillingTabs(
    [FromQuery] int branchId,
    [FromQuery] int roleId,
    [FromQuery] int tabTypeId,
    [FromQuery] int? roomServiceItemId = null)
        {
            _log.Info($"GetBillingTabs called. BranchId={branchId}, RoleId={roleId}, TabTypeId={tabTypeId}, RoomServiceItemId={roomServiceItemId?.ToString() ?? "0"}");

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

            if (roleId <= 0)
            {
                _log.Warn("Invalid RoleId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoleId must be greater than 0",
                    errors = new { roleId }
                });
            }

            if (tabTypeId <= 0 || tabTypeId > 5)
            {
                _log.Warn($"Invalid TabTypeId provided: {tabTypeId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TabTypeId must be between 1 and 5. Valid values: 1=IPD Tabs, 2=IVF Tabs, 3=Daycare Tabs, 4=Dialysis Tabs, 5=Emergency Tabs",
                    errors = new { tabTypeId }
                });
            }

            int resolvedRoomServiceItemId = roomServiceItemId.HasValue ? roomServiceItemId.Value : 0;

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _homeRepository.GetBillingTabs(branchId, roleId, tabTypeId, resolvedRoomServiceItemId, globalValues);

            if (serviceResult.Result)
                _log.Info($"Billing tabs fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No billing tabs found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateFavoriteBillingTab")]
        [Authorize]
        public IActionResult UpdateFavoriteBillingTab([FromBody] UpdateFavoriteBillingTabRequest request)
        {
            _log.Info($"UpdateFavoriteBillingTab called. BranchId={request?.BranchId}, RoleId={request?.RoleId}, TabId={request?.TabId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateFavoriteBillingTab.");
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
            var serviceResult = _homeRepository.UpdateFavoriteBillingTab(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Favorite billing tab updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Favorite billing tab update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAssignBranchRight")]
        [Authorize]
        public IActionResult GetAssignBranchRight([FromQuery] int branchId)
        {
            _log.Info($"GetAssignBranchRight called. BranchId={branchId}");

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

            var serviceResult = _homeRepository.GetAssignBranchRight(branchId);

            if (serviceResult.Result)
                _log.Info($"GetAssignBranchRight fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetAssignBranchRight fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientLedgerBill")]
        [Authorize]
        public IActionResult GetPatientLedgerBill([FromQuery] int patientId)
        {
            _log.Info($"GetPatientLedgerBill called. PatientId={patientId}");

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

            var serviceResult = _homeRepository.GetPatientLedgerBill(patientId);

            if (serviceResult.Result)
                _log.Info($"PatientLedgerBill fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientLedgerBill fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getReceiptPaymentDetails")]
        [Authorize]
        public IActionResult GetReceiptPaymentDetails([FromQuery] int receiptId)
        {
            _log.Info($"GetReceiptPaymentDetails called. ReceiptId={receiptId}");

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

            var serviceResult = _homeRepository.GetReceiptPaymentDetails(receiptId);

            if (serviceResult.Result)
                _log.Info($"Receipt payment details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Receipt payment details fetch failed: {serviceResult.Message}");

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