using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using log4net;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using HISWEBAPI.Services;
using HISWEBAPI.Configuration;
using HISWEBAPI.Utilities;
using MimeKit.Encodings;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoreController : ControllerBase
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public StoreController(
            IStoreRepository repository,
            IResponseMessageService messageService)
        {
            _storeRepository = repository;
            _messageService = messageService;
        }

        [HttpPost("createUpdateVendorMaster")]
        [Authorize]
        public IActionResult CreateUpdateVendorMaster([FromBody] CreateUpdateVendorMasterRequest request)
        {
            _log.Info($"CreateUpdateVendorMaster called. VendorName={request.VendorName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for vendor master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Pincode is optional; when given it must be 6 digits
            if (request.Pincode.HasValue && (request.Pincode < 100000 || request.Pincode > 999999))
            {
                _log.Warn($"Invalid pincode format: {request.Pincode}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Pincode must be exactly 6 digits",
                    errors = new { request.Pincode }
                });
            }

            // Additional validation for cityid, DistrictId, StateId, and CountryId
            if (request.CityId <= 0)
            {
                _log.Warn("Invalid CityId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CityId must be greater than 0",
                    errors = new { cityId = request.CityId }
                });
            }
            if (request.DistrictId <= 0)
            {
                _log.Warn("Invalid DistrictId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DistrictId must be greater than 0",
                    errors = new { districtId = request.DistrictId }
                });
            }

            if (request.StateId <= 0)
            {
                _log.Warn("Invalid StateId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "StateId must be greater than 0",
                    errors = new { stateId = request.StateId }
                });
            }

            if (request.CountryId <= 0)
            {
                _log.Warn("Invalid CountryId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CountryId must be greater than 0",
                    errors = new { countryId = request.CountryId }
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
            var serviceResult = _storeRepository.CreateUpdateVendorMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Vendor master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Vendor master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getVendorMasterList")]
        [Authorize]
        public IActionResult GetVendorMasterList(
            [FromQuery] int? vendorId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetVendorMasterList called. VendorId={vendorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _storeRepository.GetVendorMasterList(vendorId, isActive);

            if (serviceResult.Result)
                _log.Info($"Vendors fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No vendors found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>Medical Vendor Master Excel upload: SaveData=false validates the rows, SaveData=true saves them (all or nothing).</summary>
        [HttpPost("vendorExcelUpload")]
        [Authorize]
        [RequestSizeLimit(10_000_000)]
        public IActionResult VendorExcelUpload([FromBody] VendorExcelUploadRequest request)
        {
            _log.Info($"VendorExcelUpload called. Rows={request?.Rows?.Count ?? 0}, SaveData={request?.SaveData}");

            if (!ModelState.IsValid || request?.Rows == null || request.Rows.Count == 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = "No vendor rows to upload", errors = ModelState });
            }

            if (request.Rows.Count > Repositories.Implementations.StoreRepository.VendorUploadMaxRows)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = $"A file can have at most {Repositories.Implementations.StoreRepository.VendorUploadMaxRows} vendor rows"
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _storeRepository.VendorExcelUpload(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateManufacturerMaster")]
        [Authorize]
        public IActionResult CreateUpdateManufacturerMaster([FromBody] CreateUpdateManufacturerMasterRequest request)
        {
            _log.Info($"CreateUpdateManufacturerMaster called. ManufacturerId={request.ManufacturerId}, ManufacturerName={request.ManufacturerName}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.ManufacturerName))
            {
                _log.Warn("Invalid model state for manufacturer master insert/update.");
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
            var serviceResult = _storeRepository.CreateUpdateManufacturerMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Manufacturer master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Manufacturer master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getManufacturerList")]
        [Authorize]
        public IActionResult GetManufacturerList(
            [FromQuery] int? manufacturerId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetManufacturerList called. ManufacturerId={manufacturerId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _storeRepository.GetManufacturerList(manufacturerId, isActive);

            if (serviceResult.Result)
                _log.Info($"Manufacturers fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No manufacturers found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateSaltNameMaster")]
        [Authorize]
        public IActionResult CreateUpdateSaltNameMaster([FromBody] CreateUpdateSaltNameMasterRequest request)
        {
            _log.Info($"CreateUpdateSaltNameMaster called. SaltNameId={request.SaltNameId}, SaltName={request.SaltName}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.SaltName))
            {
                _log.Warn("Invalid model state for salt master insert/update.");
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
            var serviceResult = _storeRepository.CreateUpdateSaltNameMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Salt master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Salt master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSaltNameList")]
        [Authorize]
        public IActionResult GetSaltNameList(
            [FromQuery] int? saltNameId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetSaltNameList called. SaltNameId={saltNameId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _storeRepository.GetSaltNameList(saltNameId, isActive);

            if (serviceResult.Result)
                _log.Info($"Salts fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No salts found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateTaxPercentage")]
        [Authorize]
        public IActionResult CreateUpdateTaxPercentage([FromBody] CreateUpdateTaxPercentageRequest request)
        {
            _log.Info($"CreateUpdateTaxPercentage called. TaxId={request.TaxId}, Description={request.Description}, TaxValue={request.TaxValue}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Description))
            {
                _log.Warn("Invalid model state for GST slab insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (decimal.Round(request.TaxValue, 2) != request.TaxValue)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Rate can have at most 2 decimal places",
                    errors = new { taxValue = request.TaxValue }
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
            var serviceResult = _storeRepository.CreateUpdateTaxPercentage(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"GST slab operation completed: {serviceResult.Message}");
            else
                _log.Warn($"GST slab operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getTaxPercentageList")]
        [Authorize]
        public IActionResult GetTaxPercentageList(
            [FromQuery] int? taxId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetTaxPercentageList called. TaxId={taxId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _storeRepository.GetTaxPercentageList(taxId, isActive);

            if (serviceResult.Result)
                _log.Info($"GST slabs fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No GST slabs found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDrugCategory")]
        [Authorize]
        public IActionResult CreateUpdateDrugCategory([FromBody] CreateUpdateDrugCategoryRequest request)
        {
            _log.Info($"CreateUpdateDrugCategory called. DrugId={request.DrugId}, DrugName={request.DrugName}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.DrugName))
            {
                _log.Warn("Invalid model state for drug category insert/update.");
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
            var serviceResult = _storeRepository.CreateUpdateDrugCategory(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Drug category operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Drug category operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDrugCategoryList")]
        [Authorize]
        public IActionResult GetDrugCategoryList(
            [FromQuery] int? drugId = null,
            [FromQuery] int? isActive = null,
            [FromQuery] int? isNarcoticDrug = null)
        {
            _log.Info($"GetDrugCategoryList called. DrugId={drugId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}, IsNarcoticDrug={isNarcoticDrug?.ToString() ?? "All"}");

            if ((isActive.HasValue && isActive.Value != 0 && isActive.Value != 1) ||
                (isNarcoticDrug.HasValue && isNarcoticDrug.Value != 0 && isNarcoticDrug.Value != 1))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive / IsNarcoticDrug must be 0, 1, or null (All)",
                    errors = new { isActive, isNarcoticDrug }
                });
            }

            var serviceResult = _storeRepository.GetDrugCategoryList(drugId, isActive, isNarcoticDrug);

            if (serviceResult.Result)
                _log.Info($"Drug categories fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No drug categories found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateRackMaster")]
        [Authorize]
        public IActionResult CreateUpdateRackMaster([FromBody] CreateUpdateRackMasterRequest request)
        {
            _log.Info($"CreateUpdateRackMaster called. RackId={request.RackId}, RackCode={request.RackCode}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.RackCode) || string.IsNullOrWhiteSpace(request.Location))
            {
                _log.Warn("Invalid model state for rack master insert/update.");
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
            var serviceResult = _storeRepository.CreateUpdateRackMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Rack master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Rack master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getRackList")]
        [Authorize]
        public IActionResult GetRackList(
            [FromQuery] int? rackId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetRackList called. RackId={rackId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _storeRepository.GetRackList(rackId, isActive);

            if (serviceResult.Result)
                _log.Info($"Racks fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No racks found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ================= Store Item Master + its lookups =================

        private IActionResult Respond<T>(ServiceResult<T> serviceResult) =>
            StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });

        private IActionResult ValidationFailed(object? errors = null, string? message = null)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode(message == null ? "MODEL_VALIDATION_FAILED" : "INVALID_PARAMETER");
            return BadRequest(new
            {
                result = false,
                messageType = alert.Type,
                message = message ?? alert.Message,
                errors = errors ?? ModelState
            });
        }

        #region Purchase Request (2026-11-16)

        private static readonly string[] PurchaseRequestStoreTypes = { "Medical", "General" };

        private static string? NormalizePrStoreType(string? storeType) =>
            PurchaseRequestStoreTypes.FirstOrDefault(t => t.Equals(storeType?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>To Department list: active roles of the hospital</summary>
        [HttpGet("getPurchaseRequestDepartments")]
        [Authorize]
        public IActionResult GetPurchaseRequestDepartments() =>
            Respond(_storeRepository.GetPurchaseRequestDepartments(GlobalFunctions.GetGlobalValues(HttpContext)));

        /// <summary>Item search for the request grid; belowReorderLevel=true is OLD "Get Item by ROL"</summary>
        [HttpGet("searchPurchaseRequestItem")]
        [Authorize]
        public IActionResult SearchPurchaseRequestItem(
            [FromQuery] int branchId, [FromQuery] int fromDeptId, [FromQuery] string storeType = "Medical",
            [FromQuery] string? search = null, [FromQuery] bool belowReorderLevel = false)
        {
            string? type = NormalizePrStoreType(storeType);
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (fromDeptId <= 0) return ValidationFailed(new { fromDeptId }, "Please select From Department");
            if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical or General");
            if (search != null && search.Length > 100) return ValidationFailed(new { search }, "Search text is too long");
            if (!belowReorderLevel && (search?.Trim().Length ?? 0) < 2) return ValidationFailed(new { search }, "Type at least 2 characters to search");
            return Respond(_storeRepository.SearchPurchaseRequestItem(branchId, fromDeptId, type, search, belowReorderLevel, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Requests of a branch for both pages; FlagId / CanApprove are for the calling user</summary>
        [HttpGet("getPurchaseRequestList")]
        [Authorize]
        public IActionResult GetPurchaseRequestList(
            [FromQuery] int branchId, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
            [FromQuery] int? fromDeptId = null, [FromQuery] int? toDeptId = null, [FromQuery] string? storeType = null,
            [FromQuery] int? statusId = null, [FromQuery] string? prNo = null)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            DateTime from = (fromDate ?? DateTime.Today).Date, to = (toDate ?? DateTime.Today).Date;
            if (from > to) return ValidationFailed(new { fromDate, toDate }, "From Date cannot be after To Date");
            if ((to - from).TotalDays > 366) return ValidationFailed(new { fromDate, toDate }, "Date range can be at most one year");
            string? type = null;
            if (!string.IsNullOrWhiteSpace(storeType))
            {
                type = NormalizePrStoreType(storeType);
                if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical or General");
            }
            if (statusId.HasValue && statusId is not (0 or 1 or 2 or 3)) return ValidationFailed(new { statusId }, "Status must be 0 Draft, 1 Cancelled, 2 Approval Pending or 3 Approved");
            if (prNo != null && prNo.Length > 50) return ValidationFailed(new { prNo }, "PR No is too long");
            return Respond(_storeRepository.GetPurchaseRequestList(branchId, from, to, fromDeptId, toDeptId, type, statusId, prNo, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPurchaseRequestDetails")]
        [Authorize]
        public IActionResult GetPurchaseRequestDetails([FromQuery] int purchaseRequestId)
        {
            if (purchaseRequestId <= 0) return ValidationFailed(new { purchaseRequestId }, "PurchaseRequestId is required");
            return Respond(_storeRepository.GetPurchaseRequestDetails(purchaseRequestId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Current stock of one item in every department of the branch</summary>
        [HttpGet("getItemStockByDepartment")]
        [Authorize]
        public IActionResult GetItemStockByDepartment([FromQuery] int branchId, [FromQuery] int itemId)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (itemId <= 0) return ValidationFailed(new { itemId }, "ItemId is required");
            return Respond(_storeRepository.GetItemStockByDepartment(branchId, itemId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Save a new request or update a Draft (header + all items, one transaction)</summary>
        [HttpPost("savePurchaseRequest")]
        [Authorize]
        [RequestSizeLimit(2_000_000)]
        public IActionResult SavePurchaseRequest([FromBody] PurchaseRequestSaveRequest request)
        {
            _log.Info($"SavePurchaseRequest called. PurchaseRequestId={request.PurchaseRequestId}, FromDept={request.FromDeptId}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SavePurchaseRequest(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Draft -> Approval Pending</summary>
        [HttpPost("sendPurchaseRequestForApproval")]
        [Authorize]
        public IActionResult SendPurchaseRequestForApproval([FromBody] PurchaseRequestActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangePurchaseRequestStatus("send", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("cancelPurchaseRequest")]
        [Authorize]
        public IActionResult CancelPurchaseRequest([FromBody] PurchaseRequestActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangePurchaseRequestStatus("cancel", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Approve the current level; the server checks the user is an approver of that level</summary>
        [HttpPatch("approvePurchaseRequest")]
        [Authorize]
        public IActionResult ApprovePurchaseRequest([FromBody] PurchaseRequestApproveRequest request)
        {
            _log.Info($"ApprovePurchaseRequest called. PurchaseRequestId={request?.PurchaseRequestId}, Flag={request?.Flag}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ApprovePurchaseRequest(request!, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        #endregion

        #region Purchase Order (2026-11-16)

        private static bool IsValidIdCsv(string? csv, int max) =>
            !string.IsNullOrWhiteSpace(csv) && csv.Length <= 4000 &&
            csv.Split(',', StringSplitOptions.RemoveEmptyEntries) is var parts && parts.Length > 0 && parts.Length <= max &&
            parts.All(p => int.TryParse(p.Trim(), out var v) && v > 0);

        [HttpGet("getPOTermConditions")]
        [Authorize]
        public IActionResult GetPOTermConditions() =>
            Respond(_storeRepository.GetPOTermConditions(GlobalFunctions.GetGlobalValues(HttpContext)));

        /// <summary>Line defaults (last GRN rate/MRP/GST/vendor) for picked items, or OLD "Get Item by ROL" with belowReorderLevel=true</summary>
        [HttpGet("getPurchaseOrderItemDefaults")]
        [Authorize]
        public IActionResult GetPurchaseOrderItemDefaults([FromQuery] int branchId, [FromQuery] string storeType = "Medical",
            [FromQuery] string? itemIds = null, [FromQuery] bool belowReorderLevel = false)
        {
            string? type = NormalizePrStoreType(storeType);
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical or General");
            if (!belowReorderLevel && !IsValidIdCsv(itemIds, 500)) return ValidationFailed(new { itemIds }, "itemIds must be a comma separated list of item ids");
            return Respond(_storeRepository.GetPurchaseOrderItemDefaults(branchId, type, belowReorderLevel ? null : itemIds, belowReorderLevel, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>OLD "Check Purchase Request": approved requests not yet used by a PO</summary>
        [HttpGet("getApprovedPurchaseRequestsForPO")]
        [Authorize]
        public IActionResult GetApprovedPurchaseRequestsForPo([FromQuery] int branchId, [FromQuery] string storeType = "Medical",
            [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int? fromDeptId = null)
        {
            string? type = NormalizePrStoreType(storeType);
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical or General");
            DateTime from = (fromDate ?? DateTime.Today.AddDays(-30)).Date, to = (toDate ?? DateTime.Today).Date;
            if (from > to) return ValidationFailed(new { fromDate, toDate }, "From Date cannot be after To Date");
            if ((to - from).TotalDays > 366) return ValidationFailed(new { fromDate, toDate }, "Date range can be at most one year");
            return Respond(_storeRepository.GetApprovedPurchaseRequestsForPo(branchId, type, from, to, fromDeptId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPurchaseRequestItemsForPO")]
        [Authorize]
        public IActionResult GetPurchaseRequestItemsForPo([FromQuery] int branchId, [FromQuery] string? purchaseRequestIds)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (!IsValidIdCsv(purchaseRequestIds, 100)) return ValidationFailed(new { purchaseRequestIds }, "Please Select at least one Purchase Request");
            return Respond(_storeRepository.GetPurchaseRequestItemsForPo(branchId, purchaseRequestIds!, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>POs of a branch for both pages; FlagId / CanApprove / CanEdit are for the calling user</summary>
        [HttpGet("getPurchaseOrderList")]
        [Authorize]
        public IActionResult GetPurchaseOrderList(
            [FromQuery] int branchId, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
            [FromQuery] string? storeType = null, [FromQuery] int? vendorId = null, [FromQuery] int? statusId = null, [FromQuery] string? poNo = null)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            DateTime from = (fromDate ?? DateTime.Today).Date, to = (toDate ?? DateTime.Today).Date;
            if (from > to) return ValidationFailed(new { fromDate, toDate }, "From Date cannot be after To Date");
            if ((to - from).TotalDays > 366) return ValidationFailed(new { fromDate, toDate }, "Date range can be at most one year");
            string? type = null;
            if (!string.IsNullOrWhiteSpace(storeType))
            {
                type = NormalizePrStoreType(storeType);
                if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical or General");
            }
            if (statusId.HasValue && statusId is not (0 or 1 or 2 or 3)) return ValidationFailed(new { statusId }, "Status must be 0 Draft, 1 Cancelled, 2 Approval Pending or 3 Approved");
            if (poNo != null && poNo.Length > 50) return ValidationFailed(new { poNo }, "PO No is too long");
            return Respond(_storeRepository.GetPurchaseOrderList(branchId, from, to, type, vendorId, statusId, poNo, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPurchaseOrderDetails")]
        [Authorize]
        public IActionResult GetPurchaseOrderDetails([FromQuery] int poId)
        {
            if (poId <= 0) return ValidationFailed(new { poId }, "PoId is required");
            return Respond(_storeRepository.GetPurchaseOrderDetails(poId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Save new (one PO per vendor) or update a Draft; SendForApproval=true is Save &amp; Send</summary>
        [HttpPost("savePurchaseOrder")]
        [Authorize]
        [RequestSizeLimit(2_000_000)]
        public IActionResult SavePurchaseOrder([FromBody] PurchaseOrderSaveRequest request)
        {
            _log.Info($"SavePurchaseOrder called. PoId={request?.PoId}, Lines={request?.Items?.Count ?? 0}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SavePurchaseOrder(request!, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("sendPurchaseOrderForApproval")]
        [Authorize]
        public IActionResult SendPurchaseOrderForApproval([FromBody] PurchaseOrderActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangePurchaseOrderStatus("send", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("cancelPurchaseOrder")]
        [Authorize]
        public IActionResult CancelPurchaseOrder([FromBody] PurchaseOrderActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangePurchaseOrderStatus("cancel", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPatch("approvePurchaseOrder")]
        [Authorize]
        public IActionResult ApprovePurchaseOrder([FromBody] PurchaseOrderApproveRequest request)
        {
            _log.Info($"ApprovePurchaseOrder called. PoId={request?.PoId}, Flag={request?.Flag}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ApprovePurchaseOrder(request!, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>PO document (OLD fileUpload); returns the DMS path to send back in savePurchaseOrder.DocumentPath. View with Home/getFile.</summary>
        [HttpPost("uploadPurchaseOrderDocument")]
        [Authorize]
        [RequestSizeLimit(30_000_000)]
        public IActionResult UploadPurchaseOrderDocument([FromForm] PurchaseOrderDocumentRequest request)
        {
            if (!ModelState.IsValid || request.UploadFile == null || request.UploadFile.Length == 0)
                return ValidationFailed(new { uploadFile = "File cannot be empty" }, "Document file is required");
            var helper = HttpContext.RequestServices.GetRequiredService<FileUploadHelper>();
            var (ok, filePath, error) = helper.UploadFile(request.UploadFile, "UploadedDocument");
            if (!ok)
            {
                var fail = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return BadRequest(new { result = false, messageType = fail.Type, message = error, data = (object?)null });
            }
            var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
            return Ok(new { result = true, messageType = alert.Type, message = "Document uploaded", data = new PurchaseOrderDocumentModel { DocumentPath = filePath } });
        }

        #endregion

        private static bool IsValidActiveFilter(int? isActive) => !isActive.HasValue || isActive.Value == 0 || isActive.Value == 1;

        [HttpPost("createUpdatePurchaseUnit")]
        [Authorize]
        public IActionResult CreateUpdatePurchaseUnit([FromBody] CreateUpdatePurchaseUnitRequest request)
        {
            _log.Info($"CreateUpdatePurchaseUnit called. Id={request.PurchaseUnitId}, Name={request.PurchaseUnitName}");
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.PurchaseUnitName)) return ValidationFailed();
            return Respond(_storeRepository.CreateUpdatePurchaseUnit(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPurchaseUnitList")]
        [Authorize]
        public IActionResult GetPurchaseUnitList([FromQuery] int? isActive = null)
        {
            if (!IsValidActiveFilter(isActive)) return ValidationFailed(new { isActive }, "IsActive must be 0, 1, or null (All)");
            return Respond(_storeRepository.GetPurchaseUnitList(isActive));
        }

        [HttpPost("createUpdateSaleUnit")]
        [Authorize]
        public IActionResult CreateUpdateSaleUnit([FromBody] CreateUpdateSaleUnitRequest request)
        {
            _log.Info($"CreateUpdateSaleUnit called. Id={request.SaleUnitId}, Name={request.SaleUnitName}");
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.SaleUnitName)) return ValidationFailed();
            return Respond(_storeRepository.CreateUpdateSaleUnit(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getSaleUnitList")]
        [Authorize]
        public IActionResult GetSaleUnitList([FromQuery] int? isActive = null)
        {
            if (!IsValidActiveFilter(isActive)) return ValidationFailed(new { isActive }, "IsActive must be 0, 1, or null (All)");
            return Respond(_storeRepository.GetSaleUnitList(isActive));
        }

        [HttpPost("createUpdateDosageForm")]
        [Authorize]
        public IActionResult CreateUpdateDosageForm([FromBody] CreateUpdateDosageFormRequest request)
        {
            _log.Info($"CreateUpdateDosageForm called. Id={request.DosageFormId}, Name={request.DosageFormName}");
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.DosageFormName)) return ValidationFailed();
            return Respond(_storeRepository.CreateUpdateDosageForm(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getDosageFormList")]
        [Authorize]
        public IActionResult GetDosageFormList([FromQuery] int? isActive = null)
        {
            if (!IsValidActiveFilter(isActive)) return ValidationFailed(new { isActive }, "IsActive must be 0, 1, or null (All)");
            return Respond(_storeRepository.GetDosageFormList(isActive));
        }

        [HttpPost("createUpdateStoreItemMaster")]
        [Authorize]
        public IActionResult CreateUpdateStoreItemMaster([FromBody] CreateUpdateStoreItemMasterRequest request)
        {
            _log.Info($"CreateUpdateStoreItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}");

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Name)) return ValidationFailed();

            if (request.CategoryId != 6 && request.CategoryId != 7 && request.CategoryId != 15)
                return ValidationFailed(new { request.CategoryId }, "Store Type must be Medical, General or Blood Bank Inventory");

            if (request.GstPer.HasValue && decimal.Round(request.GstPer.Value, 2) != request.GstPer.Value)
                return ValidationFailed(new { request.GstPer }, "GST can have at most 2 decimal places");

            // Medical items: salt, dosage form, HSN code and GST slab are mandatory (owner decision 2026-11-13; pack size dropped later)
            if (request.CategoryId == 6 &&
                ((request.DescriptionId ?? 0) <= 0 || (request.DosageFormId ?? 0) <= 0 ||
                 string.IsNullOrWhiteSpace(request.HsnCode) || !request.GstPer.HasValue))
                return ValidationFailed(null, "Salt / Generic, Dosage Form, HSN Code and GST Slab are required for Medical items");

            var serviceResult = _storeRepository.CreateUpdateStoreItemMaster(request, GlobalFunctions.GetGlobalValues(HttpContext));

            if (serviceResult.Result)
                _log.Info($"Store item operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Store item operation failed: {serviceResult.Message}");

            return Respond(serviceResult);
        }

        [HttpGet("getStoreItemList")]
        [Authorize]
        public IActionResult GetStoreItemList(
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? isActive = null)
        {
            if (!IsValidActiveFilter(isActive)) return ValidationFailed(new { isActive }, "IsActive must be 0, 1, or null (All)");
            if (categoryId.HasValue && categoryId != 6 && categoryId != 7 && categoryId != 15)
                return ValidationFailed(new { categoryId }, "CategoryId must be 6, 7 or 15");

            return Respond(_storeRepository.GetStoreItemList(search, categoryId, isActive));
        }

        [HttpGet("getStoreItemDetails")]
        [Authorize]
        public IActionResult GetStoreItemDetails([FromQuery] int serviceItemId)
        {
            if (serviceItemId <= 0) return ValidationFailed(new { serviceItemId }, "ServiceItemId must be greater than 0");
            return Respond(_storeRepository.GetStoreItemDetails(serviceItemId));
        }

        // ================= Stock Receiving (GRN) =================

        private static readonly string[] GrnStoreTypes = { "Medical", "General", "Blood Bank Inventory" };

        [HttpGet("getStockReceivingStores")]
        [Authorize]
        public IActionResult GetStockReceivingStores([FromQuery] int branchId)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            return Respond(_storeRepository.GetStockReceivingStores(branchId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("searchReceivingItem")]
        [Authorize]
        public IActionResult SearchReceivingItem([FromQuery] string storeType = "Medical", [FromQuery] string? search = null)
        {
            string? type = GrnStoreTypes.FirstOrDefault(t => t.Equals(storeType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical, General or Blood Bank Inventory");
            if (search != null && search.Length > 100) return ValidationFailed(new { search }, "Search text is too long");
            return Respond(_storeRepository.SearchReceivingItem(type, search, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getStockReceivingList")]
        [Authorize]
        public IActionResult GetStockReceivingList(
            [FromQuery] int branchId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? vendorId = null,
            [FromQuery] int? statusId = null,
            [FromQuery] string? search = null)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (statusId.HasValue && (statusId < 1 || statusId > 3)) return ValidationFailed(new { statusId }, "StatusId must be 1 (Draft), 2 (Posted), 3 (Cancelled) or null (All)");
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate) return ValidationFailed(new { fromDate, toDate }, "From Date cannot be after To Date");
            return Respond(_storeRepository.GetStockReceivingList(branchId, fromDate, toDate, vendorId, statusId, search, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getStockReceivingDetails")]
        [Authorize]
        public IActionResult GetStockReceivingDetails([FromQuery] int receivingId)
        {
            if (receivingId <= 0) return ValidationFailed(new { receivingId }, "ReceivingId must be greater than 0");
            return Respond(_storeRepository.GetStockReceivingDetails(receivingId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Autosave of the GRN being entered - Redis only, per user (Previous Records on the GRN page).</summary>
        [HttpPost("saveGrnUnsavedDraft")]
        [Authorize]
        [RequestSizeLimit(2_000_000)]
        public IActionResult SaveGrnUnsavedDraft([FromBody] GrnUnsavedDraftSaveRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SaveGrnUnsavedDraft(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getGrnUnsavedDrafts")]
        [Authorize]
        public IActionResult GetGrnUnsavedDrafts([FromQuery] int branchId)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            return Respond(_storeRepository.GetGrnUnsavedDrafts(branchId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("deleteGrnUnsavedDraft")]
        [Authorize]
        public IActionResult DeleteGrnUnsavedDraft([FromBody] GrnUnsavedDraftDeleteRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.DeleteGrnUnsavedDraft(request.DraftId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Scanned barcode -> store item(s) of the store type (earlier GRN lots, then the item master barcode).</summary>
        [HttpGet("getItemByBarcode")]
        [Authorize]
        public IActionResult GetItemByBarcode([FromQuery] string storeType = "Medical", [FromQuery] string? barcode = null)
        {
            string? type = GrnStoreTypes.FirstOrDefault(t => t.Equals(storeType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type == null) return ValidationFailed(new { storeType }, "Store Type must be Medical, General or Blood Bank Inventory");
            if (string.IsNullOrWhiteSpace(barcode)) return ValidationFailed(new { barcode }, "Barcode is required");
            if (barcode.Trim().Length > 100) return ValidationFailed(new { barcode }, "Barcode cannot exceed 100 characters");
            return Respond(_storeRepository.GetItemByBarcode(type, barcode, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Item-wise previous purchase history (OLD "Last Purchase History" popup on the GRN page).</summary>
        [HttpGet("getItemPurchaseHistory")]
        [Authorize]
        public IActionResult GetItemPurchaseHistory([FromQuery] int itemId, [FromQuery] int? excludeReceivingId = null)
        {
            if (itemId <= 0) return ValidationFailed(new { itemId }, "ItemId must be greater than 0");
            return Respond(_storeRepository.GetItemPurchaseHistory(itemId, excludeReceivingId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Save a new GRN or update a Draft GRN (header + all lines, one transaction). Saved GRNs stay Draft until posted.</summary>
        [HttpPost("saveStockReceiving")]
        [Authorize]
        [RequestSizeLimit(5_000_000)]
        public IActionResult SaveStockReceiving([FromBody] StockReceivingSaveRequest request)
        {
            _log.Info($"SaveStockReceiving called. ReceivingId={request.ReceivingId}, VendorId={request.VendorId}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SaveStockReceiving(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("postStockReceiving")]
        [Authorize]
        public IActionResult PostStockReceiving([FromBody] StockReceivingActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangeStockReceivingStatus("post", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("unpostStockReceiving")]
        [Authorize]
        public IActionResult UnpostStockReceiving([FromBody] StockReceivingActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangeStockReceivingStatus("unpost", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPost("cancelStockReceiving")]
        [Authorize]
        public IActionResult CancelStockReceiving([FromBody] StockReceivingActionRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.ChangeStockReceivingStatus("cancel", request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        // ================= Patient Medicine Issue (pharmacy sale / IPD issue, 2026-11-23) =================

        /// <summary>patientTypeId 1 = OPD (registered patients, min 2 chars), 2 = admitted IPD patients.</summary>
        [HttpGet("searchPatientForSale")]
        [Authorize]
        public IActionResult SearchPatientForSale([FromQuery] int branchId, [FromQuery] int patientTypeId = 1, [FromQuery] string? search = null)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (patientTypeId != 1 && patientTypeId != 2) return ValidationFailed(new { patientTypeId }, "PatientTypeId must be 1 (OPD) or 2 (IPD)");
            if (search != null && search.Length > 100) return ValidationFailed(new { search }, "Search text is too long");
            return Respond(_storeRepository.SearchPatientForSale(branchId, patientTypeId, search, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>searchType 1 = name, 2 = barcode / code, 3 = salt (OLD S_SearchMadicineItem).</summary>
        [HttpGet("searchSaleItem")]
        [Authorize]
        public IActionResult SearchSaleItem([FromQuery] int branchId, [FromQuery] int roleId, [FromQuery] string? search, [FromQuery] int searchType = 1)
        {
            if (branchId <= 0 || roleId <= 0) return ValidationFailed(new { branchId, roleId }, "Branch and store are required");
            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2 || search.Length > 100)
                return ValidationFailed(new { search }, "Enter 2 to 100 characters to search");
            if (searchType < 1 || searchType > 3) return ValidationFailed(new { searchType }, "SearchType must be 1, 2 or 3");
            return Respond(_storeRepository.SearchSaleItem(branchId, roleId, search, searchType, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>FEFO batches with stock of one or more items (comma-separated ids).</summary>
        [HttpGet("getSaleItemBatches")]
        [Authorize]
        public IActionResult GetSaleItemBatches([FromQuery] int branchId, [FromQuery] int roleId, [FromQuery] string? itemIds)
        {
            if (branchId <= 0 || roleId <= 0) return ValidationFailed(new { branchId, roleId }, "Branch and store are required");
            var ids = (itemIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0 || ids.Length > 200 || ids.Any(i => !int.TryParse(i, out var v) || v <= 0))
                return ValidationFailed(new { itemIds }, "ItemIds must be 1-200 comma-separated ids");
            return Respond(_storeRepository.GetSaleItemBatches(branchId, roleId, string.Join(",", ids), GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPharmacyUserRights")]
        [Authorize]
        public IActionResult GetPharmacyUserRights([FromQuery] int branchId, [FromQuery] int roleId)
        {
            if (branchId <= 0 || roleId <= 0) return ValidationFailed(new { branchId, roleId }, "Branch and store are required");
            return Respond(_storeRepository.GetPharmacyUserRights(branchId, roleId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPharmacySaleList")]
        [Authorize]
        public IActionResult GetPharmacySaleList([FromQuery] int branchId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate,
            [FromQuery] int? roleId = null, [FromQuery] int? patientTypeId = null, [FromQuery] string? search = null)
        {
            if (branchId <= 0) return ValidationFailed(new { branchId }, "BranchId must be greater than 0");
            if (fromDate == default || toDate == default || fromDate.Date > toDate.Date) return ValidationFailed(new { fromDate, toDate }, "Select a valid date range");
            if ((toDate.Date - fromDate.Date).TotalDays > 366) return ValidationFailed(new { fromDate, toDate }, "Date range cannot exceed one year");
            return Respond(_storeRepository.GetPharmacySaleList(branchId, roleId, fromDate, toDate, patientTypeId, search, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPharmacySaleDetails")]
        [Authorize]
        public IActionResult GetPharmacySaleDetails([FromQuery] int storeBillId)
        {
            if (storeBillId <= 0) return ValidationFailed(new { storeBillId }, "StoreBillId must be greater than 0");
            return Respond(_storeRepository.GetPharmacySaleDetails(storeBillId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPharmacyRequestCounts")]
        [Authorize]
        public IActionResult GetPharmacyRequestCounts([FromQuery] int branchId, [FromQuery] int roleId)
        {
            if (branchId <= 0 || roleId <= 0) return ValidationFailed(new { branchId, roleId }, "Branch and store are required");
            return Respond(_storeRepository.GetPharmacyRequestCounts(branchId, roleId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>type = emr (doctor prescriptions) | indent (ward indents) | discharge (take-home medicines); statusId 1 pending, 2 done, empty = all.</summary>
        [HttpGet("getPharmacyRequests")]
        [Authorize]
        public IActionResult GetPharmacyRequests([FromQuery] string type, [FromQuery] int branchId, [FromQuery] int roleId,
            [FromQuery] int? statusId = null, [FromQuery] string? search = null)
        {
            type = (type ?? "").Trim().ToLowerInvariant();
            if (type != "emr" && type != "indent" && type != "discharge") return ValidationFailed(new { type }, "Type must be emr, indent or discharge");
            if (branchId <= 0 || roleId <= 0) return ValidationFailed(new { branchId, roleId }, "Branch and store are required");
            if (search != null && search.Length > 100) return ValidationFailed(new { search }, "Search text is too long");
            return Respond(_storeRepository.GetPharmacyRequests(type, branchId, roleId, statusId, search, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpGet("getPharmacyRequestItems")]
        [Authorize]
        public IActionResult GetPharmacyRequestItems([FromQuery] string type, [FromQuery] int branchId, [FromQuery] int roleId, [FromQuery] int requestId)
        {
            type = (type ?? "").Trim().ToLowerInvariant();
            if (type != "emr" && type != "indent" && type != "discharge") return ValidationFailed(new { type }, "Type must be emr, indent or discharge");
            if (branchId <= 0 || roleId <= 0 || requestId <= 0) return ValidationFailed(new { branchId, roleId, requestId }, "Branch, store and request are required");
            return Respond(_storeRepository.GetPharmacyRequestItems(type, branchId, roleId, requestId, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>OPD / walk-in sale or IPD issue (one transaction): visit, bill, FT + lines, stock, receipt, indent bookkeeping.</summary>
        [HttpPost("savePharmacySale")]
        [Authorize]
        public IActionResult SavePharmacySale([FromBody] PharmacySaleSaveRequest request)
        {
            _log.Info($"SavePharmacySale called. Type={request.PatientTypeId}, PatientId={request.PatientId}, Lines={request.Items?.Count ?? 0}");
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SavePharmacySale(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        [HttpPatch("rejectPatientIndent")]
        [Authorize]
        public IActionResult RejectPatientIndent([FromBody] RejectPatientIndentRequest request)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Reason)) return ValidationFailed();
            return Respond(_storeRepository.RejectPatientIndent(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Count a bill slip print (first = original, later = duplicate reprint).</summary>
        [HttpPatch("printPharmacyBill")]
        [Authorize]
        public IActionResult PrintPharmacyBill([FromBody] PharmacyBillPrintRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.PrintPharmacyBill(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }

        /// <summary>Raise a ward (IPD) medicine indent to a store.</summary>
        [HttpPost("savePatientMedicineIndent")]
        [Authorize]
        public IActionResult SavePatientMedicineIndent([FromBody] SavePatientMedicineIndentRequest request)
        {
            if (!ModelState.IsValid) return ValidationFailed();
            return Respond(_storeRepository.SavePatientMedicineIndent(request, GlobalFunctions.GetGlobalValues(HttpContext)));
        }
    }
}