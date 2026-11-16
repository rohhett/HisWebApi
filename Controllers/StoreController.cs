using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using log4net;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using HISWEBAPI.Services;
using HISWEBAPI.Configuration;
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
    }
}