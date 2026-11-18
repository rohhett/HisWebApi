using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    //[Route("mob/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public UserController(
            IUserRepository repository,
            IResponseMessageService messageService)
        {
            _userRepository = repository;
            _messageService = messageService;
        }

     

        [HttpPost("userLogin")]
        [AllowAnonymous]
        public IActionResult UserLogin([FromBody] UserLoginRequest request)
        {
            _log.Info($"UserLogin called. BranchId={request.BranchId}, UserName={request.UserName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for user login.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.UserLogin(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                HttpContext.Items["ClientType"] as string); // "Web", or the app's X-Client-Type header

            if (serviceResult.Result)
                _log.Info($"User login successful: {serviceResult.Message}");
            else
                _log.Warn($"User login failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout([FromBody] LogoutRequest request)
        {
            _log.Info("Logout called.");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for logout.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // the token being logged out: its id (jti), expiry (exp) and login session (sessionId)
            string? jti = User.FindFirst("jti")?.Value;
            long.TryParse(User.FindFirst("sessionId")?.Value, out long tokenSessionId);
            DateTime expiresUtc = long.TryParse(User.FindFirst("exp")?.Value, out long exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
                : DateTime.UtcNow.AddDays(1); // no exp claim: keep the block long enough

            var serviceResult = _userRepository.Logout(request ?? new LogoutRequest(), tokenSessionId, jti, expiresUtc);

            if (serviceResult.Result)
                _log.Info($"User logout successful: {serviceResult.Message}");
            else
                _log.Warn($"User logout failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }

        [HttpPost("newUserSignUp")]
        [AllowAnonymous]
        public IActionResult NewUserSignUp([FromBody] UserSignupRequest request)
        {
            _log.Info($"NewUserSignUp called. UserName={request.UserName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for user signup.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.NewUserSignUp(request);

            if (serviceResult.Result)
                _log.Info($"User signup successful: {serviceResult.Message}");
            else
                _log.Warn($"User signup failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("sendSmsOtp")]
        [AllowAnonymous]
        public IActionResult SendSmsOtp([FromBody] SendSmsOtpRequest request)
        {
            _log.Info($"SendSmsOtp called. UserName={request.UserName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for send SMS OTP.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.SendSmsOtp(request);

            if (serviceResult.Result)
                _log.Info($"SMS OTP sent successfully: {serviceResult.Message}");
            else
                _log.Warn($"SMS OTP send failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("verifySmsOtp")]
        [AllowAnonymous]
        public IActionResult VerifySmsOtp([FromBody] VerifySmsOtpRequest request)
        {
            _log.Info($"VerifySmsOtp called. UserId={request.UserId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for verify SMS OTP.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.VerifySmsOtp(request);

            if (serviceResult.Result)
                _log.Info($"SMS OTP verified successfully: {serviceResult.Message}");
            else
                _log.Warn($"SMS OTP verification failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("sendEmailOtp")]
        [AllowAnonymous]
        public IActionResult SendEmailOtp([FromBody] SendEmailOtpRequest request)
        {
            _log.Info($"SendEmailOtp called. UserName={request.UserName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for send email OTP.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.SendEmailOtp(request);

            if (serviceResult.Result)
                _log.Info($"Email OTP sent successfully: {serviceResult.Message}");
            else
                _log.Warn($"Email OTP send failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("verifyEmailOtp")]
        [AllowAnonymous]
        public IActionResult VerifyEmailOtp([FromBody] VerifyEmailOtpRequest request)
        {
            _log.Info($"VerifyEmailOtp called. UserId={request.UserId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for verify email OTP.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.VerifyEmailOtp(request);

            if (serviceResult.Result)
                _log.Info($"Email OTP verified successfully: {serviceResult.Message}");
            else
                _log.Warn($"Email OTP verification failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("resetPasswordByUserId")]
        [AllowAnonymous]
        public IActionResult ResetPasswordByUserId([FromBody] ResetPasswordRequest request)
        {
            _log.Info($"ResetPasswordByUserId called. UserId={request.UserId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for reset password.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.ResetPasswordByUserId(request);

            if (serviceResult.Result)
                _log.Info($"Password reset successful: {serviceResult.Message}");
            else
                _log.Warn($"Password reset failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }

        [HttpPatch("updatePassword")]
        [Authorize]
        public IActionResult UpdatePassword([FromBody] UpdatePasswordRequest request)
        {
            _log.Info($"UpdatePassword called. UserId={request.UserId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for update password.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _userRepository.UpdatePassword(request);

            if (serviceResult.Result)
                _log.Info($"Password updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Password update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message
            });
        }


        [HttpGet("getUserRoles")]
        [Authorize]
        public IActionResult GetUserRoles([FromQuery] int branchId)
        {
            _log.Info($"GetUserRoles called. ");
            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            if (globalValues.userId <= 0)
            {
                _log.Warn($"Invalid global values. UserId={globalValues.userId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_TOKEN");
                return Unauthorized(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Invalid user session. Please login again.",
                    errors = new { userId = globalValues.userId }
                });
            }

            var request = new UserRoleRequest { BranchId = branchId, UserId = globalValues.userId };
            var serviceResult = _userRepository.GetUserRoles(request);

            if (serviceResult.Result)
                _log.Info($"User roles fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User roles fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getUserTabAndSubMenuMapping")]
        [Authorize]
        public IActionResult GetUserTabAndSubMenuMapping([FromQuery] int branchId, int roleId)
        {
            _log.Info($"GetUserTabAndSubMenuMapping called");

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            if (globalValues.userId <= 0)
            {
                _log.Warn($"Invalid global values. UserId={globalValues.userId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_TOKEN");
                return Unauthorized(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Invalid user session. Please login again.",
                    errors = new { userId = globalValues.userId }
                });
            }

            _log.Info($"Processing request with UserId={globalValues.userId}, BranchId={branchId}, RoleId={roleId}");

            var serviceResult = _userRepository.GetUserTabAndSubMenuMapping(roleId, branchId, globalValues.userId);

            if (serviceResult.Result)
                _log.Info($"User tab/menu mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User tab/menu mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveUserFavoriteRoles")]
        [Authorize]
        public IActionResult SaveUserFavoriteRoles([FromBody] SaveUserFavoriteRolesRequest request)
        {
            _log.Info($"SaveUserFavoriteRoles called. BranchId={request.BranchId}, UserId={request.UserId}, RoleIds Count={request.RoleIds.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for save user favorite roles.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate BranchId
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

            // Validate UserId
            if (request.UserId <= 0)
            {
                _log.Warn("Invalid UserId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UserId must be greater than 0",
                    errors = new { userId = request.UserId }
                });
            }

            // Validate RoleIds list (can be empty to remove all favorites)
            if (request.RoleIds == null)
            {
                _log.Warn("RoleIds list is null.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoleIds list cannot be null (use empty list to remove all favorites)",
                    errors = new { roleIds = "null" }
                });
            }

            // Validate each RoleId in the list
            if (request.RoleIds.Any(roleId => roleId <= 0))
            {
                _log.Warn("One or more invalid RoleIds provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All RoleIds must be greater than 0",
                    errors = new { invalidRoleIds = request.RoleIds.Where(r => r <= 0).ToList() }
                });
            }

            // Check for duplicate RoleIds
            var duplicateRoleIds = request.RoleIds
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateRoleIds.Any())
            {
                _log.Warn($"Duplicate RoleIds found: {string.Join(", ", duplicateRoleIds)}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Duplicate RoleIds are not allowed",
                    errors = new { duplicateRoleIds }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _userRepository.SaveUserFavoriteRoles(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User favorite roles saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User favorite roles save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveRoleWiseUserFavoriteSubMenu")]
        [Authorize]
        public IActionResult SaveRoleWiseUserFavoriteSubMenu([FromBody] SaveRoleWiseUserFavoriteSubMenuRequest request)
        {
            _log.Info($"SaveRoleWiseUserFavoriteSubMenu called. BranchId={request.BranchId}, UserId={request.UserId}, RoleId={request.RoleId}, SubMenuId={request.SubMenuId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for save favorite submenu.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate UserId
            if (request.UserId <= 0)
            {
                _log.Warn("Invalid UserId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UserId must be greater than 0",
                    errors = new { userId = request.UserId }
                });
            }

            // Validate RoleId
            if (request.RoleId <= 0)
            {
                _log.Warn("Invalid RoleId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoleId must be greater than 0",
                    errors = new { roleId = request.RoleId }
                });
            }

            // Validate BranchId
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

            // Validate SubMenuId
            if (request.SubMenuId <= 0)
            {
                _log.Warn("Invalid SubMenuId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "SubMenuId must be greater than 0",
                    errors = new { subMenuId = request.SubMenuId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _userRepository.SaveRoleWiseUserFavoriteSubMenu(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Favorite submenu saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Favorite submenu save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserAccessRights")]
        [Authorize]
        public IActionResult GetUserAccessRights(
    [FromQuery] int branchId,
    [FromQuery] int roleId)
        {
            _log.Info($"GetUserAccessRights called. BranchId={branchId}, RoleId={roleId}");

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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            if (globalValues.userId <= 0)
            {
                _log.Warn($"Invalid user session. UserId={globalValues.userId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_TOKEN");
                return Unauthorized(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Invalid user session. Please login again.",
                    errors = new { userId = globalValues.userId }
                });
            }

            var serviceResult = _userRepository.GetUserAccessRights(branchId, roleId, globalValues);

            if (serviceResult.Result)
                _log.Info($"User access rights fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User access rights fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDashboardUserAccessRights")]
        [Authorize]
        public IActionResult GetDashboardUserAccessRights(
    [FromQuery] int branchId,
    [FromQuery] int roleId)
        {
            _log.Info($"GetDashboardUserAccessRights called. BranchId={branchId}, RoleId={roleId}");

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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            if (globalValues.userId <= 0)
            {
                _log.Warn($"Invalid user session. UserId={globalValues.userId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_TOKEN");
                return Unauthorized(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Invalid user session. Please login again.",
                    errors = new { userId = globalValues.userId }
                });
            }

            var serviceResult = _userRepository.GetDashboardUserAccessRights(branchId, roleId, globalValues);

            if (serviceResult.Result)
                _log.Info($"Dashboard user access rights fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Dashboard user access rights fetch failed: {serviceResult.Message}");

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