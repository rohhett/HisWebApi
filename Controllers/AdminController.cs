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
using Microsoft.Extensions.Caching.Distributed;
using MimeKit;
using System;
using System.Linq;
using System.Net;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AdminController(
            IAdminRepository repository,
            IResponseMessageService messageService)
        {
            _adminRepository = repository;
            _messageService = messageService;
        }



        [HttpPost("createUpdateRoleMaster")]
        [Authorize]
        public IActionResult CreateUpdateRoleMaster([FromBody] RoleMasterRequest request)
        {
            _log.Info("CreateUpdateRoleMaster called.");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for role insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateRoleMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Role operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Role operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPatch("updateRoleMasterStatus")]
        [Authorize]
        public IActionResult UpdateRoleMasterStatus([FromQuery] int roleId, [FromQuery] int isActive)
        {
            _log.Info($"UpdateRoleMasterStatus called. RoleId={roleId}, IsActive={isActive}");

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
            var serviceResult = _adminRepository.UpdateRoleMasterStatus(roleId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"Role status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Role status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


      
        [HttpGet("roleMasterList")]
        [Authorize]
        public IActionResult RoleMasterList([FromQuery] int? roleId = null)
        {
            _log.Info($"RoleMasterList called. RoleId={roleId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.RoleMasterList(roleId);

            if (serviceResult.Result)
                _log.Info($"Roles fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No roles found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getFaIconList")]
        [Authorize]
        public IActionResult getFaIconMaster()
        {
            _log.Info("getFaIconList called.");
            var serviceResult = _adminRepository.getFaIconMaster();

            if (serviceResult.Result)
                _log.Info($"icon fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No icon found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("CreateUpdateUserMaster")]
        [Authorize]
        public IActionResult CreateUpdateUserMaster([FromBody] UserMasterRequest request)
        {
            _log.Info($"CreateUpdateUserMaster called. UserName={request.UserName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CreateUpdateUserMaster.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var serviceResult = _adminRepository.CreateUpdateUserMaster(request);

            if (serviceResult.Result)
                _log.Info($"CreateUpdateUserMaster successful: {serviceResult.Message}");
            else
                _log.Warn($"CreateUpdateUserMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateUserMasterStatus")]
        [Authorize]
        public IActionResult UpdateUserMasterStatus([FromQuery] int userId, [FromQuery] int isActive)
        {
            _log.Info($"UpdateUserMasterStatus called. UserId={userId}, IsActive={isActive}");

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
            var serviceResult = _adminRepository.UpdateUserMasterStatus(userId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"User status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"User status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       


        [HttpGet("userMasterList")]
        [Authorize]
        public IActionResult UserMasterList([FromQuery] int? userId = null)
        {
            _log.Info($"UserMasterList called. UserId={userId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.UserMasterList(userId);

            if (serviceResult.Result)
                _log.Info($"Users fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No users found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateUserDepartment")]
        [Authorize]
        public IActionResult CreateUpdateUserDepartment([FromBody] UserDepartmentRequest request)
        {
            _log.Info("CreateUpdateUserDepartment called.");
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for department insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateUserDepartment(request, globalValues);
            if (serviceResult.Result)
                _log.Info($"Department operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Department operation failed: {serviceResult.Message}");
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateUserDepartmentStatus")]
        [Authorize]
        public IActionResult UpdateUserDepartmentStatus([FromQuery] int id, [FromQuery] int isActive)
        {
            _log.Info($"UpdateUserDepartmentStatus called. Id={id}, IsActive={isActive}");

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
            var serviceResult = _adminRepository.UpdateUserDepartmentStatus(id, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"Department status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Department status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("userDepartmentList")]
        [Authorize]
        public IActionResult UserDepartmentList([FromQuery] int? id = null)
        {
            _log.Info($"UserDepartmentList called. Id={id?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.UserDepartmentList(id);

            if (serviceResult.Result)
                _log.Info($"Departments fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No departments found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateUserGroupMaster")]
        [Authorize]
        public IActionResult CreateUpdateUserGroupMaster([FromBody] UserGroupRequest request)
        {
            _log.Info("CreateUpdateUserGroupMaster called.");
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for group insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateUserGroupMaster(request, globalValues);
            if (serviceResult.Result)
                _log.Info($"Group operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Group operation failed: {serviceResult.Message}");
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        //[HttpGet("userGroupList")]
        //[Authorize]
        //public IActionResult UserGroupList()
        //{
        //    _log.Info("UserGroupList called.");
        //    var serviceResult = _adminRepository.UserGroupList();
        //    if (serviceResult.Result)
        //        _log.Info($"Groups fetched successfully: {serviceResult.Message}");
        //    else
        //        _log.Warn($"No groups found: {serviceResult.Message}");
        //    return StatusCode(serviceResult.StatusCode, new
        //    {
        //        result = serviceResult.Result,
        //        messageType = serviceResult.MessageType,
        //        message = serviceResult.Message,
        //        data = serviceResult.Data
        //    });
        //}

        [HttpPatch("updateUserGroupStatus")]
        [Authorize]
        public IActionResult UpdateUserGroupStatus([FromQuery] int id, [FromQuery] int isActive)
        {
            _log.Info($"UpdateUserGroupStatus called. Id={id}, IsActive={isActive}");

            if (id <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Id must be greater than 0"
                });
            }

            if (isActive != 0 && isActive != 1)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsActive must be 0 or 1"
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.UpdateUserGroupStatus(id, isActive, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("userGroupList")]
        [Authorize]
        public IActionResult UserGroupList([FromQuery] int? id = null)
        {
            _log.Info($"UserGroupList called. Id={id?.ToString() ?? "All"}");
            var serviceResult = _adminRepository.UserGroupList(id);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPost("createUpdateUserGroupMembers")]
        [Authorize]
        public IActionResult CreateUpdateUserGroupMembers([FromBody] UserGroupMembersRequest request)
        {
            _log.Info("CreateUpdateUserGroupMembers called.");
            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for group members insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateUserGroupMembers(request, globalValues);
            if (serviceResult.Result)
                _log.Info($"Group members operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Group members operation failed: {serviceResult.Message}");
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("userGroupMembersList")]
        [Authorize]
        public IActionResult UserGroupMembersList([FromQuery] int? groupId)
        {
            _log.Info("UserGroupMembersList called.");
            if (groupId == null || groupId <= 0)
            {
                _log.Warn("Invalid GroupId supplied.");
                return BadRequest(new
                {
                    result = false,
                    messageType = "ERROR",
                    message = "GroupId is mandatory and must be greater than 0.",
                    data = ""
                });
            }

            var serviceResult = _adminRepository.UserGroupMembersList(groupId);
            if (serviceResult.Result)
                _log.Info($"Group members fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No group members found: {serviceResult.Message}");
            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }





        [HttpPost("saveUpdateRoleMapping")]
        [Authorize]
        public IActionResult SaveUpdateRoleMapping([FromBody] UserRoleMappingListRequest request)
        {
            _log.Info("SaveUpdateRoleMapping called.");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for user role mapping save/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate that all items (if any exist) have the same userId, branchId, and typeId as the parent request
            if (request.userRoleMappings != null && request.userRoleMappings.Count > 0)
            {
                bool isConsistent = request.userRoleMappings.All(x =>
                    x.userId == request.userId &&
                    x.branchId == request.branchId &&
                    x.typeId == request.typeId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent userId, branchId, or typeId in role mapping list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All role mapping items must have the same userId, branchId, and typeId as the request"
                    });
                }

                _log.Info($"Saving user role mapping for UserId={request.userId}, BranchId={request.branchId}, TypeId={request.typeId}, Count={request.userRoleMappings.Count}");
            }
            else
            {
                _log.Info($"Removing all roles for UserId={request.userId}, BranchId={request.branchId}, TypeId={request.typeId}");
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _adminRepository.SaveUpdateRoleMapping(
                request.userId,
                request.branchId,
                request.typeId,
                request.userRoleMappings ?? new List<UserRoleMappingRequest>(),
                globalValues
            );

            if (serviceResult.Result)
                _log.Info($"User role mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User role mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAssignRoleForUserAuthorization")]
        [Authorize]
        public IActionResult GetAssignRoleForUserAuthorization([FromQuery] int branchId, [FromQuery] int typeId, [FromQuery] int userId)
        {
            _log.Info($"GetAssignRoleForUserAuthorization called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetAssignRoleForUserAuthorization.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId, TypeId, and UserId must be greater than 0"
                });
            }

            var serviceResult = _adminRepository.GetAssignRoleForUserAuthorization(branchId, typeId, userId);

            if (serviceResult.Result)
                _log.Info($"Role authorization data fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No role authorization data found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("saveUpdateUserRightMapping")]
        [Authorize]
        public IActionResult SaveUpdateUserRightMapping([FromBody] SaveUserRightMappingRequest request)
        {
            _log.Info($"SaveUpdateUserRightMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, UserRights Count={request.UserRights.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserRightMapping.");
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

            var serviceResult = _adminRepository.SaveUpdateUserRightMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User right mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User right mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAssignUserRightMapping")]
        [Authorize]
        public IActionResult GetAssignUserRightMapping(
          [FromQuery] int branchId,
          [FromQuery] int typeId,
          [FromQuery] int userId,
          [FromQuery] int roleId)
        {
            _log.Info($"GetAssignUserRightMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetAssignUserRightMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId, roleId }
                });
            }

            var serviceResult = _adminRepository.GetAssignUserRightMapping(branchId, typeId, userId, roleId);

            if (serviceResult.Result)
                _log.Info($"User right mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User right mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }





        [HttpPost("saveUpdateDashBoardUserRightMapping")]
        [Authorize]
        public IActionResult SaveUpdateDashBoardUserRightMapping([FromBody] SaveDashboardUserRightMappingRequest request)
        {
            _log.Info($"SaveUpdateDashBoardUserRightMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, DashboardUserRights Count={request.DashboardUserRights.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateDashBoardUserRightMapping.");
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

            var serviceResult = _adminRepository.SaveUpdateDashBoardUserRightMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Dashboard user right mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Dashboard user right mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getAssignDashBoardUserRight")]
        [Authorize]
        public IActionResult GetAssignDashBoardUserRight(
              [FromQuery] int branchId,
              [FromQuery] int typeId,
              [FromQuery] int userId,
              [FromQuery] int roleId)
        {
            _log.Info($"GetAssignDashBoardUserRight called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0 )
            {
                _log.Warn("Invalid parameters for GetAssignDashBoardUserRight.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId, roleId }
                });
            }

            var serviceResult = _adminRepository.GetAssignDashBoardUserRight(branchId, typeId, userId, roleId);

            if (serviceResult.Result)
                _log.Info($"Dashboard user right mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Dashboard user right mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateNavigationTabMaster")]
        [Authorize]
        public IActionResult CreateUpdateNavigationTabMaster([FromBody] NavigationTabMasterRequest request)
        {
            _log.Info("CreateUpdateNavigationTabMaster called.");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for navigation tab insert/update.");
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

            var serviceResult = _adminRepository.CreateUpdateNavigationTabMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Navigation tab operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Navigation tab operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getNavigationTabMaster")]
        [Authorize]
        public IActionResult GetNavigationTabMaster()
        {
            _log.Info("GetNavigationTabMaster endpoint called.");

            var serviceResult = _adminRepository.GetNavigationTabMaster();

            if (serviceResult.Result)
                _log.Info($"Navigation tabs fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No navigation tabs found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateNavigationSubMenuMaster")]
        [Authorize]
        public IActionResult CreateUpdateNavigationSubMenuMaster([FromBody] NavigationSubMenuMasterRequest request)
        {
            _log.Info($"CreateUpdateNavigationSubMenuMaster called. TabId={request.TabId}, SubMenuName={request.SubMenuName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for navigation sub menu insert/update.");
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

            var serviceResult = _adminRepository.CreateUpdateNavigationSubMenuMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Navigation sub menu operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Navigation sub menu operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getNavigationSubMenuMaster")]
        [Authorize]
        public IActionResult GetNavigationSubMenuMaster()
        {
            _log.Info($"GetNavigationSubMenuMaster called");

            var serviceResult = _adminRepository.GetNavigationSubMenuMaster();

            if (serviceResult.Result)
                _log.Info($"Navigation sub menus fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Navigation sub menus fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPost("saveUpdateRoleWiseMenuMapping")]
        [Authorize]
        public IActionResult SaveUpdateRoleWiseMenuMapping([FromBody] SaveRoleWiseMenuMappingRequest request)
        {
            _log.Info($"SaveUpdateRoleWiseMenuMapping called. BranchId={request.BranchId}, RoleId={request.RoleId}, IsFirst={request.IsFirst}, MenuMappings Count={request.MenuMappings.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateRoleWiseMenuMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsFirst parameter
            if (request.IsFirst != 0 && request.IsFirst != 1)
            {
                _log.Warn($"Invalid IsFirst parameter: {request.IsFirst}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsFirst must be either 0 or 1",
                    errors = new { IsFirst = request.IsFirst }
                });
            }

            // Validate that all items (if any exist) have the same branchId and roleId as the parent request
            if (request.MenuMappings != null && request.MenuMappings.Count > 0)
            {
                bool isConsistent = request.MenuMappings.All(x =>
                    x.BranchId == request.BranchId &&
                    x.RoleId == request.RoleId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent branchId or roleId in menu mapping list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All menu mapping items must have the same branchId and roleId as the request"
                    });
                }

                _log.Info($"Saving role-wise menu mapping for BranchId={request.BranchId}, RoleId={request.RoleId}, Count={request.MenuMappings.Count}");
            }
            else
            {
                if (request.IsFirst == 1)
                {
                    _log.Info($"Removing all menu mappings for BranchId={request.BranchId}, RoleId={request.RoleId}");
                }
                else
                {
                    _log.Info($"No menu mappings to save for BranchId={request.BranchId}, RoleId={request.RoleId}");
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _adminRepository.SaveUpdateRoleWiseMenuMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Role-wise menu mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Role-wise menu mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getRoleWiseMenuMapping")]
        [Authorize]
        public IActionResult GetRoleWiseMenuMapping(
          [FromQuery] int branchId,
          [FromQuery] int roleId)
        {
            _log.Info($"GetRoleWiseMenuMapping called. BranchId={branchId}, RoleId={roleId}");

            if (branchId <= 0 || roleId <= 0)
            {
                _log.Warn("Invalid parameters for GetRoleWiseMenuMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, roleId) must be greater than 0",
                    errors = new { branchId, roleId }
                });
            }

            var serviceResult = _adminRepository.GetRoleWiseMenuMapping(branchId, roleId);

            if (serviceResult.Result)
                _log.Info($"Role-wise menu mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Role-wise menu mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }





        [HttpPost("saveUpdateUserMenuMaster")]
        [Authorize]
        public IActionResult SaveUpdateUserMenuMaster([FromBody] SaveUserMenuMasterRequest request)
        {
            _log.Info($"SaveUpdateUserMenuMaster called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, IsFirst={request.IsFirst}, UserMenus Count={request.UserMenus.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserMenuMaster.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsFirst parameter
            if (request.IsFirst != 0 && request.IsFirst != 1)
            {
                _log.Warn($"Invalid IsFirst parameter: {request.IsFirst}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsFirst must be either 0 or 1",
                    errors = new { IsFirst = request.IsFirst }
                });
            }

            // Validate that all items (if any exist) have the same typeId, userId, branchId, and roleId as the parent request
            if (request.UserMenus != null && request.UserMenus.Count > 0)
            {
                bool isConsistent = request.UserMenus.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId &&
                    x.RoleId == request.RoleId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent typeId, userId, branchId, or roleId in user menu list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All user menu items must have the same typeId, userId, branchId, and roleId as the request"
                    });
                }

                _log.Info($"Saving user menu for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, Count={request.UserMenus.Count}");
            }
            

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _adminRepository.SaveUpdateUserMenuMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User menu master saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User menu master save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserWiseMenuMaster")]
        [Authorize]
        public IActionResult GetUserWiseMenuMaster(
        [FromQuery] int branchId,
        [FromQuery] int typeId,
        [FromQuery] int userId,
        [FromQuery] int roleId)
        {
            _log.Info($"GetUserWiseMenuMaster called. BranchId={branchId}, TypeId={typeId}, UserId={userId},RoleId={roleId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetUserWiseMenuMaster.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId , roleId }
                });
            }

            var serviceResult = _adminRepository.GetUserWiseMenuMaster(branchId, typeId, userId, roleId);

            if (serviceResult.Result)
                _log.Info($"User-wise menu (granted + remaining) fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User-wise menu fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("saveUpdateUserCorporateMapping")]
        [Authorize]
        public IActionResult SaveUpdateUserCorporateMapping([FromBody] SaveUserCorporateMappingRequest request)
        {
            _log.Info($"SaveUpdateUserCorporateMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, IsFirst={request.IsFirst}, UserCorporates Count={request.UserCorporates.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserCorporateMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsFirst parameter
            if (request.IsFirst != 0 && request.IsFirst != 1)
            {
                _log.Warn($"Invalid IsFirst parameter: {request.IsFirst}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsFirst must be either 0 or 1",
                    errors = new { IsFirst = request.IsFirst }
                });
            }

            // Validate that all items (if any exist) have the same typeId, userId, and branchId as the parent request
            if (request.UserCorporates != null && request.UserCorporates.Count > 0)
            {
                bool isConsistent = request.UserCorporates.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent typeId, userId, or branchId in user corporate mapping list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All user corporate mapping items must have the same typeId, userId, and branchId as the request"
                    });
                }

                _log.Info($"Saving user corporate mapping for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, Count={request.UserCorporates.Count}");
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _adminRepository.SaveUpdateUserCorporateMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User corporate mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User corporate mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserWiseCorporateMapping")]
        [Authorize]
        public IActionResult GetUserWiseCorporateMapping(
            [FromQuery] int branchId,
            [FromQuery] int typeId,
            [FromQuery] int userId)
        {
            _log.Info($"GetUserWiseCorporateMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetUserWiseCorporateMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId }
                });
            }

            var serviceResult = _adminRepository.GetUserWiseCorporateMapping(branchId, typeId, userId);

            if (serviceResult.Result)
                _log.Info($"User corporate mapping (granted + remaining) fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User corporate mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveUpdateUserBedMapping")]
        [Authorize]
        public IActionResult SaveUpdateUserBedMapping([FromBody] SaveUserBedMappingRequest request)
        {
            _log.Info($"SaveUpdateUserBedMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, IsFirst={request.IsFirst}, UserBeds Count={request.UserBeds.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserBedMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsFirst parameter
            if (request.IsFirst != 0 && request.IsFirst != 1)
            {
                _log.Warn($"Invalid IsFirst parameter: {request.IsFirst}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsFirst must be either 0 or 1",
                    errors = new { IsFirst = request.IsFirst }
                });
            }

            // Validate that all items (if any exist) have the same typeId, userId, and branchId as the parent request
            if (request.UserBeds != null && request.UserBeds.Count > 0)
            {
                bool isConsistent = request.UserBeds.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent typeId, userId, or branchId in user bed mapping list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All user bed mapping items must have the same typeId, userId, and branchId as the request"
                    });
                }

                _log.Info($"Saving user bed mapping for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, Count={request.UserBeds.Count}");
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _adminRepository.SaveUpdateUserBedMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User bed mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User bed mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserWiseBedMapping")]
        [Authorize]
        public IActionResult GetUserWiseBedMapping(
            [FromQuery] int branchId,
            [FromQuery] int typeId,
            [FromQuery] int userId)
        {
            _log.Info($"GetUserWiseBedMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetUserWiseBedMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId }
                });
            }

            var serviceResult = _adminRepository.GetUserWiseBedMapping(branchId, typeId, userId);

            if (serviceResult.Result)
                _log.Info($"User bed mapping (granted + remaining) fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User bed mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateBranchMaster")]
        [Authorize]
        public IActionResult CreateUpdateBranchMaster([FromBody] BranchMasterRequest request)
        {
            _log.Info($"CreateUpdateBranchMaster called. BranchName={request.BranchName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for branch insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBranchMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Branch operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Branch operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBranchDetails")]
        [Authorize]
        public IActionResult GetBranchDetails([FromQuery] int? branchId = null)
        {
            _log.Info($"GetBranchDetails called. BranchId={branchId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.GetBranchDetails(branchId);

            if (serviceResult.Result)
                _log.Info($"Branches fetched successfully from cache: {serviceResult.Message}");
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



        [HttpPost("createUpdateStateMaster")]
        [Authorize]
        public IActionResult CreateUpdateStateMaster([FromBody] CreateUpdateStateMasterRequest request)
        {
            _log.Info($"CreateUpdateStateMaster called. StateId={request.StateId}, StateName={request.StateName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for state master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation for CountryId
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
            var serviceResult = _adminRepository.CreateUpdateStateMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"State master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"State master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = new { stateId = serviceResult.Data }
            });
        }

        [HttpPost("createUpdateDistrictMaster")]
        [Authorize]
        public IActionResult CreateUpdateDistrictMaster([FromBody] CreateUpdateDistrictMasterRequest request)
        {
            _log.Info($"CreateUpdateDistrictMaster called. DistrictId={request.DistrictId}, DistrictName={request.DistrictName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for district master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation for StateId and CountryId
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
            var serviceResult = _adminRepository.CreateUpdateDistrictMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"District master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"District master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = new { districtId = serviceResult.Data }
            });
        }

        [HttpPost("createUpdateCityMaster")]
        [Authorize]
        public IActionResult CreateUpdateCityMaster([FromBody] CreateUpdateCityMasterRequest request)
        {
            _log.Info($"CreateUpdateCityMaster called. CityId={request.CityId}, CityName={request.CityName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for city master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation for DistrictId, StateId, and CountryId
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
            var serviceResult = _adminRepository.CreateUpdateCityMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"City master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"City master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = new { cityId = serviceResult.Data }
            });
        }


        [HttpPost("createUpdatePincodeMaster")]
        [Authorize]
        public IActionResult CreateUpdatePincodeMaster([FromBody] CreateUpdatePincodeMasterRequest request)
        {
            _log.Info($"CreateUpdatePincodeMaster called. PincodeId={request.PincodeId}, CityId={request.CityId}, Pincode={request.Pincode}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for pincode master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation for CityId
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

            // Validate Pincode is exactly 6 digits
            if (request.Pincode < 100000 || request.Pincode > 999999)
            {
                _log.Warn($"Invalid Pincode provided: {request.Pincode}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Pincode must be exactly 6 digits",
                    errors = new { pincode = request.Pincode }
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
            var serviceResult = _adminRepository.CreateUpdatePincodeMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Pincode master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Pincode master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = new { pincodeId = serviceResult.Data }
            });
        }

        [HttpPost("createUpdateHeaderMaster")]
        [Authorize]
        public IActionResult CreateUpdateHeaderMaster([FromBody] HeaderMasterRequest request)
        {
            _log.Info($"CreateUpdateHeaderMaster called. HeaderId={request.HeaderId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for header insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate IsHeader value
            if (request.IsHeader != 0 && request.IsHeader != 1)
            {
                _log.Warn("Invalid IsHeader value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsHeader must be 0 or 1",
                    errors = new { isHeader = request.IsHeader }
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
            var serviceResult = _adminRepository.CreateUpdateHeaderMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Header operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Header operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getHeaderMaster")]
        [Authorize]
        public IActionResult GetHeaderMaster(
            [FromQuery] int branchId,
            [FromQuery] int roleId,
            [FromQuery] int typeId,
            [FromQuery] int isHeader)
        {
            _log.Info($"GetHeaderMaster called. BranchId={branchId}, RoleId={roleId}, TypeId={typeId}, IsHeader={isHeader}");

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

            if (roleId < 0)
            {
                _log.Warn("Invalid RoleId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoleId must be greater than Equal to 0",
                    errors = new { roleId }
                });
            }

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

            if (isHeader != 0 && isHeader != 1)
            {
                _log.Warn("Invalid IsHeader value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsHeader must be 0 or 1",
                    errors = new { isHeader }
                });
            }

            var serviceResult = _adminRepository.GetHeaderMaster(branchId, roleId, typeId, isHeader);

            if (serviceResult.Result)
                _log.Info($"Headers fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No headers found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       

        [HttpGet("getSequenceTypeList")]
        [Authorize]
        public IActionResult GetSequenceTypeList()
        {
            _log.Info("GetSequenceTypeList called.");

            var serviceResult = _adminRepository.GetSequenceTypeList();

            if (serviceResult.Result)
                _log.Info($"Sequence types fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No sequence types found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateSequenceMaster")]
        [Authorize]
        public IActionResult CreateUpdateSequenceMaster([FromBody] CreateUpdateSequenceMasterRequest request)
        {
            _log.Info($"CreateUpdateSequenceMaster called. SequenceId={request.SequenceId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for sequence master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate TypeId (must be greater than 0)
            if (request.TypeId <= 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId = request.TypeId }
                });
            }

            // Length validation is handled by [Range] attribute in the DTO
            // No need for additional validation here since ModelState will catch it

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateSequenceMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Sequence master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Sequence master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSequenceMaster")]
        [Authorize]
        public IActionResult GetSequenceMaster([FromQuery] int sequenceTypeId)
        {
            _log.Info($"GetSequenceMaster called. SequenceTypeId={sequenceTypeId}");

            // Validate sequenceTypeId
            if (sequenceTypeId <= 0)
            {
                _log.Warn("Invalid SequenceTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "SequenceTypeId must be greater than 0",
                    errors = new { sequenceTypeId }
                });
            }

            var serviceResult = _adminRepository.GetSequenceMaster(sequenceTypeId);

            if (serviceResult.Result)
                _log.Info($"Sequences fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No sequences found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      

        [HttpPost("createUpdateBranchSequenceMapping")]
        [Authorize]
        public IActionResult CreateUpdateBranchSequenceMapping([FromBody] CreateUpdateBranchSequenceMappingRequest request)
        {
            _log.Info($"CreateUpdateBranchSequenceMapping called. MappingId={request.MappingId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}, SequenceId={request.SequenceId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for branch sequence mapping insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBranchSequenceMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Branch sequence mapping operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Branch sequence mapping operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBranchSequenceMapping")]
        [Authorize]
        public IActionResult GetBranchSequenceMapping()
        {
            _log.Info("GetBranchSequenceMapping called.");

            var serviceResult = _adminRepository.GetBranchSequenceMapping();

            if (serviceResult.Result)
                _log.Info($"Branch sequence mappings fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No branch sequence mappings found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPost("createUpdateLabReportLetterHead")]
        [Authorize]
        public IActionResult CreateUpdateLabReportLetterHead([FromForm] LabReportLetterHeadRequest request)
        {
            _log.Info($"CreateUpdateLabReportLetterHead called. Id={request.Id}, BranchId={request.BranchId}, TypeId={request.TypeId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for lab report letter head insert/update.");
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
            if (request.BranchId < 0)
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

            // Validate TypeId
            if (request.TypeId < 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId = request.TypeId }
                });
            }

            // Validate file upload for new records (Id = 0)
            if (request.Id == 0 && (request.LetterHeadFile == null || request.LetterHeadFile.Length == 0))
            {
                _log.Warn("Letter head file is required for new records.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Letter head file is required for new letter head configuration",
                    errors = new { letterHeadFile = "Required" }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateLabReportLetterHead(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Lab report letter head operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Lab report letter head operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getLabReportLetterHeadList")]
        [Authorize]
        public IActionResult GetLabReportLetterHeadList()
        {
            _log.Info("GetLabReportLetterHeadList called.");

            var serviceResult = _adminRepository.GetLabReportLetterHeadList();

            if (serviceResult.Result)
                _log.Info($"Lab report letter heads fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No lab report letter heads found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("deleteLetterHeadMaster")]
        [Authorize]
        public IActionResult DeleteLetterHeadMaster([FromQuery] int id)
        {
            _log.Info($"DeleteLetterHeadMaster API called. Id={id}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided for letter head deletion.");
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
            var serviceResult = _adminRepository.DeleteLetterHeadMaster(id, globalValues);

            if (serviceResult.Result)
                _log.Info($"Letter head deleted successfully: {serviceResult.Message}");
            else
                _log.Warn($"Letter head deletion failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDoctorSignatureMaster")]
        [Authorize]
        public IActionResult CreateUpdateDoctorSignatureMaster([FromForm] DoctorSignatureMasterRequest request)
        {
            _log.Info($"CreateUpdateDoctorSignatureMaster called. Id={request.Id}, BranchId={request.BranchId}, DoctorId={request.DoctorId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor signature insert/update.");
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
            if (request.BranchId < 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than or equal to 0",
                    errors = new { branchId = request.BranchId }
                });
            }

            // Validate DoctorId
            if (request.DoctorId <= 0)
            {
                _log.Warn("Invalid DoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DoctorId must be greater than 0",
                    errors = new { doctorId = request.DoctorId }
                });
            }

            // Validate file upload for new records (Id = 0)
            if (request.Id == 0 && (request.DocSignFile == null || request.DocSignFile.Length == 0))
            {
                _log.Warn("Doctor signature file is required for new records.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Doctor signature file is required for new signature configuration",
                    errors = new { docSignFile = "Required" }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateDoctorSignatureMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor signature operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Doctor signature operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDoctorSignatureMasterList")]
        [Authorize]
        public IActionResult GetDoctorSignatureMasterList()
        {
            _log.Info("GetDoctorSignatureMasterList called.");

            var serviceResult = _adminRepository.GetDoctorSignatureMasterList();

            if (serviceResult.Result)
                _log.Info($"Doctor signatures fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No doctor signatures found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("deleteDoctorSignatureMaster")]
        [Authorize]
        public IActionResult DeleteDoctorSignatureMaster([FromQuery] int id)
        {
            _log.Info($"DeleteDoctorSignatureMaster API called. Id={id}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided for doctor signature deletion.");
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
            var serviceResult = _adminRepository.DeleteDoctorSignatureMaster(id, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor signature deleted successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor signature deletion failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateBankMaster")]
        [Authorize]
        public IActionResult CreateUpdateBankMaster([FromBody] BankMasterRequest request)
        {
            _log.Info($"CreateUpdateBankMaster called. BankId={request.BankId}, BankName={request.BankName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for bank insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBankMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Bank operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Bank operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBankList")]
        [Authorize]
        public IActionResult GetBankList([FromQuery] int? bankId = null, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetBankList called. BankId={bankId?.ToString() ?? "All"}");
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
            var serviceResult = _adminRepository.GetBankList(bankId, isActive);

            if (serviceResult.Result)
                _log.Info($"Banks fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No banks found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateBankDetailMaster")]
        [Authorize]
        public IActionResult CreateUpdateBankDetailMaster([FromBody] BankDetailMasterRequest request)
        {
            _log.Info($"CreateUpdateBankDetailMaster called. BankId={request.BankId}, BankName={request.BankName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for bank detail insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBankDetailMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Bank detail operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Bank detail operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBankDetailList")]
        [Authorize]
        public IActionResult GetBankDetailList([FromQuery] int? bankId = null, [FromQuery] int? isActive = null)
        {
            _log.Info($"GetBankDetailList called. BankId={bankId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _adminRepository.GetBankDetailList(bankId, isActive);

            if (serviceResult.Result)
                _log.Info($"Bank details fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No bank details found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #region MRD Room Master APIs

       
        [HttpPost("createUpdateMRDRoomMaster")]
        [Authorize]
        public IActionResult CreateUpdateMRDRoomMaster([FromBody] MRDRoomMasterRequest request)
        {
            _log.Info($"CreateUpdateMRDRoomMaster called. RoomId={request?.RoomId}, Name={request?.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for MRD Room insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateMRDRoomMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"MRD Room operation completed: {serviceResult.Message}");
            else
                _log.Warn($"MRD Room operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

      
        [HttpGet("getMRDRoomMaster")]
        [Authorize]
        public IActionResult GetMRDRoomMaster(
            [FromQuery] int? roomId = 0,
            [FromQuery] int? activeFlag = 0)
        {
            _log.Info($"GetMRDRoomMaster called. RoomId={roomId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

            // Validate activeFlag if provided
            if (activeFlag.HasValue && activeFlag.Value < 0 && activeFlag.Value > 2)
            {
                _log.Warn($"Invalid ActiveFlag value: {activeFlag.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ActiveFlag must be 0 (All), 1 (Active), or 2 (Inactive)",
                    errors = new { activeFlag }
                });
            }

            var serviceResult = _adminRepository.GetMRDRoomMaster(roomId, activeFlag);

            if (serviceResult.Result)
                _log.Info($"MRD Rooms fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"MRD Rooms fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region MRD Rack Master APIs

       
        [HttpPost("createUpdateMRDRackMaster")]
        [Authorize]
        public IActionResult CreateUpdateMRDRackMaster([FromBody] MRDRackMasterRequest request)
        {
            _log.Info($"CreateUpdateMRDRackMaster called. RackId={request?.RackId}, RoomId={request?.RoomId}, Name={request?.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for MRD Rack insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateMRDRackMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"MRD Rack operation completed: {serviceResult.Message}");
            else
                _log.Warn($"MRD Rack operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       
        [HttpGet("getMRDRackMaster")]
        [Authorize]
        public IActionResult GetMRDRackMaster(
            [FromQuery] int roomId,
            [FromQuery] int? rackId = 0,
            [FromQuery] int? activeFlag = 0)
        {
            _log.Info($"GetMRDRackMaster called. RoomId={roomId}, RackId={rackId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

            // Validate roomId
            if (roomId <= 0)
            {
                _log.Warn("Invalid RoomId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoomId must be greater than 0",
                    errors = new { roomId }
                });
            }

            // Validate activeFlag if provided
            if (activeFlag.HasValue && activeFlag.Value < 0 && activeFlag.Value > 2)
            {
                _log.Warn($"Invalid ActiveFlag value: {activeFlag.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ActiveFlag must be 0 (All), 1 (Active), or 2 (Inactive)",
                    errors = new { activeFlag }
                });
            }

            var serviceResult = _adminRepository.GetMRDRackMaster(roomId, rackId, activeFlag);

            if (serviceResult.Result)
                _log.Info($"MRD Racks fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"MRD Racks fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region MRD Shelf Master APIs

        /// <summary>
        /// Create or Update MRD Shelf Master
        /// </summary>
        [HttpPost("createUpdateMRDShelfMaster")]
        [Authorize]
        public IActionResult CreateUpdateMRDShelfMaster([FromBody] MRDShelfMasterRequest request)
        {
            _log.Info($"CreateUpdateMRDShelfMaster called. ShelfId={request?.ShelfId}, RoomId={request?.RoomId}, RackId={request?.RackId}, Name={request?.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for MRD Shelf insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateMRDShelfMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"MRD Shelf operation completed: {serviceResult.Message}");
            else
                _log.Warn($"MRD Shelf operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get MRD Shelf Master
        /// </summary>
        /// <param name="roomId">Required: Room ID</param>
        /// <param name="rackId">Required: Rack ID</param>
        /// <param name="shelfId">Optional: Specific Shelf ID (0 or null for all shelves in the rack)</param>
        /// <param name="activeFlag">Optional: 0=All, 1=Active only, 2=Inactive only</param>
        [HttpGet("getMRDShelfMaster")]
        [Authorize]
        public IActionResult GetMRDShelfMaster(
            [FromQuery] int roomId,
            [FromQuery] int rackId,
            [FromQuery] int? shelfId = 0,
            [FromQuery] int? activeFlag = 0)
        {
            _log.Info($"GetMRDShelfMaster called. RoomId={roomId}, RackId={rackId}, ShelfId={shelfId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

            // Validate roomId
            if (roomId <= 0)
            {
                _log.Warn("Invalid RoomId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RoomId must be greater than 0",
                    errors = new { roomId }
                });
            }

            // Validate rackId
            if (rackId <= 0)
            {
                _log.Warn("Invalid RackId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RackId must be greater than 0",
                    errors = new { rackId }
                });
            }

            // Validate activeFlag if provided
            if (activeFlag.HasValue && activeFlag.Value < 0 && activeFlag.Value > 2)
            {
                _log.Warn($"Invalid ActiveFlag value: {activeFlag.Value}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ActiveFlag must be 0 (All), 1 (Active), or 2 (Inactive)",
                    errors = new { activeFlag }
                });
            }

            var serviceResult = _adminRepository.GetMRDShelfMaster(roomId, rackId, shelfId, activeFlag);

            if (serviceResult.Result)
                _log.Info($"MRD Shelves fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"MRD Shelves fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion


        [HttpPost("createUpdatePatientDocumentMaster")]
        [Authorize]
        public IActionResult CreateUpdatePatientDocumentMaster([FromBody] PatientDocumentMasterRequest request)
        {
            _log.Info($"CreateUpdatePatientDocumentMaster called. DocumentId={request.DocumentId}, DocumentName={request.DocumentName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for patient document insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdatePatientDocumentMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Patient document operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Patient document operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientDocumentMaster")]
        [Authorize]
        public IActionResult GetPatientDocumentMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetPatientDocumentMaster called. IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _adminRepository.GetPatientDocumentMaster(isActive);

            if (serviceResult.Result)
                _log.Info($"Patient documents fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No patient documents found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOutSourceLabMasterList")]
        [Authorize]
        public IActionResult GetOutSourceLabMasterList([FromQuery] int? isActive = null)
        {
            _log.Info($"GetOutSourceLabMasterList API called. isActive={isActive?.ToString() ?? "null (all)"}");

            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "isActive must be 0 or 1."
                });
            }


            var serviceResult = _adminRepository.GetOutSourceLabMasterList(isActive);

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

        [HttpPost("saveOutSourceLabMaster")]
        [Authorize]
        public IActionResult SaveOutSourceLabMaster([FromBody] SaveOutSourceLabMasterRequest request)
        {
            _log.Info($"SaveOutSourceLabMaster API called. OutSourceLabId={request?.OutSourceLabId}, " +
                      $"OutSourceLab={request?.OutSourceLab}");

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

            if (request.IsActive != 0 && request.IsActive != 1)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "IsActive must be 0 or 1."
                });
            }
            if (request.branchId <= 0)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "branchId must be greater than 0"
                });
            }


            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveOutSourceLabMaster(request, globalValues);

            if (!serviceResult.Result)
                _log.Warn($"SaveOutSourceLabMaster failed: {serviceResult.Message} " +
                          $"(StatusCode={serviceResult.StatusCode})");
            else
                _log.Info($"SaveOutSourceLabMaster succeeded: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getRateListMaster")]
        [Authorize]
        public IActionResult GetRateListMaster([FromQuery] GetRateListMasterRequest request)
        {
            _log.Info($"GetRateListMaster called. RateListName={request.RateListName ?? "All"}, IsActive={request.IsActive?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.GetRateListMaster(request.RateListName, request.IsActive);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateRateListMaster")]
        [Authorize]
        public IActionResult CreateUpdateRateListMaster([FromBody] CreateUpdateRateListMasterRequest request)
        {
            _log.Info($"CreateUpdateRateListMaster called. RateListId={request.RateListId}, RateListName={request.RateListName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for RateListMaster insert/update.");
                var validationAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validationAlert.Type,
                    message = validationAlert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateRateListMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"RateListMaster operation successful: {serviceResult.Message}");
            else
                _log.Warn($"RateListMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getTariffMaster")]
        [Authorize]
        public IActionResult GetTariffMaster(
    [FromQuery] string rateListId,
    [FromQuery] string patientType = "OPD",
    [FromQuery] string bedTypeId = "0",
    [FromQuery] string doctorId = "0",
    [FromQuery] string categoryId = "0",
    [FromQuery] string subCategoryId = "0",
    [FromQuery] string subSubCategoryId = "0",
    [FromQuery] string serviceItemId = "0",
    [FromQuery] string serviceName = null)
        {
            _log.Info("GetTariffMaster called.");

            if (string.IsNullOrWhiteSpace(rateListId) || !int.TryParse(rateListId, out int parsedRateListId) || parsedRateListId <= 0)
            {
                _log.Warn("rateListId is required and must be greater than 0.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "rateListId is required and must be greater than 0."
                });
            }

            if (!int.TryParse(categoryId, out int parsedCategoryId) || parsedCategoryId <= 0)
            {
                var v = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = v.Type,
                    message = "categoryId must be greater than 0"
                });
            }


            var serviceResult = _adminRepository.GetTariffMaster(
                rateListId, patientType, bedTypeId, doctorId,
                categoryId, subCategoryId, subSubCategoryId,
                serviceItemId, serviceName);

            if (serviceResult.Result)
                _log.Info($"GetTariffMaster succeeded: {serviceResult.Message}");
            else
                _log.Warn($"GetTariffMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateTariffMaster")]
        [Authorize]
        public IActionResult CreateUpdateTariffMaster([FromBody] CreateUpdateTariffMasterRequest request)
        {
            _log.Info("CreateUpdateTariffMaster called.");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CreateUpdateTariffMaster.");
                var validAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validAlert.Type,
                    message = validAlert.Message,
                    errors = ModelState
                });
            }

            if (request.TariffMasterData == null || !request.TariffMasterData.Any())
            {
                _log.Warn("TariffMasterData list is empty.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TariffMasterData cannot be empty."
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateTariffMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CreateUpdateTariffMaster succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CreateUpdateTariffMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateInsuranceCompanyMaster")]
        [Authorize]
        public IActionResult CreateUpdateInsuranceCompanyMaster([FromBody] InsuranceCompanyMasterRequest request)
        {
            _log.Info($"CreateUpdateInsuranceCompanyMaster called. InsuranceCompanyId={request.InsuranceCompanyId}, InsuranceCompanyName={request.InsuranceCompanyName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for insurance company insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // For update, InsuranceCompanyId must be > 0
            if (request.InsuranceCompanyId < 0)
            {
                _log.Warn("Invalid InsuranceCompanyId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "InsuranceCompanyId must be 0 (for create) or greater than 0 (for update)",
                    errors = new { insuranceCompanyId = request.InsuranceCompanyId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateInsuranceCompanyMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Insurance company operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Insurance company operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getInsuranceCompanyMasterList")]
        [Authorize]
        public IActionResult GetInsuranceCompanyMasterList()
        {
            _log.Info("GetInsuranceCompanyMasterList called.");

            var serviceResult = _adminRepository.GetInsuranceCompanyMasterList();

            if (serviceResult.Result)
                _log.Info($"Insurance companies fetched successfully from cache: {serviceResult.Message}");
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


        [HttpPost("createUpdateCorporateTypeMaster")]
        [Authorize]
        public IActionResult CreateUpdateCorporateTypeMaster([FromBody] CorporateTypeMasterRequest request)
        {
            _log.Info($"CreateUpdateCorporateTypeMaster called. CorporateTypeId={request.CorporateTypeId}, CorporateTypeName={request.CorporateTypeName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for corporate type insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.CorporateTypeId < 0)
            {
                _log.Warn("Invalid CorporateTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateTypeId must be 0 (for create) or greater than 0 (for update)",
                    errors = new { corporateTypeId = request.CorporateTypeId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateCorporateTypeMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Corporate type operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Corporate type operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateTypeMasterList")]
        [Authorize]
        public IActionResult GetCorporateTypeMasterList()
        {
            _log.Info("GetCorporateTypeMasterList called.");

            var serviceResult = _adminRepository.GetCorporateTypeMasterList();

            if (serviceResult.Result)
                _log.Info($"Corporate types fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No corporate types found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateCorporateMaster")]
        [Authorize]
        public IActionResult CreateUpdateCorporateMaster([FromBody] CorporateMasterRequest request)
        {
            _log.Info($"CreateUpdateCorporateMaster called. CorporateId={request.CorporateId}, CorporateName={request.CorporateName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for corporate master insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.CorporateId < 0)
            {
                _log.Warn("Invalid CorporateId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateId must be 0 (for create) or greater than 0 (for update)",
                    errors = new { corporateId = request.CorporateId }
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
            var serviceResult = _adminRepository.CreateUpdateCorporateMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Corporate master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Corporate master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateMasterList")]
        [Authorize]
        public IActionResult GetCorporateMasterList(
            [FromQuery] int? corporateId = null,
            [FromQuery] string corporateName = null,
            [FromQuery] int? insuranceCompanyId = null,
            [FromQuery] string insuranceCompanyName = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetCorporateMasterList called. CorporateId={corporateId?.ToString() ?? "All"}, CorporateName={corporateName ?? "All"}, InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}, InsuranceCompanyName={insuranceCompanyName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

            // Validate IsActive if provided
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

            var serviceResult = _adminRepository.GetCorporateMasterList(
                corporateId,
                corporateName,
                insuranceCompanyId,
                insuranceCompanyName,
                isActive);

            if (serviceResult.Result)
                _log.Info($"Corporates fetched successfully from cache: {serviceResult.Message}");
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

        [HttpPatch("updateCorporateMasterStatus")]
        [Authorize]
        public IActionResult UpdateCorporateMasterStatus([FromQuery] int corporateId, [FromQuery] int isActive)
        {
            _log.Info($"UpdateCorporateMasterStatus called. CorporateId={corporateId}, IsActive={isActive}");

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
            var serviceResult = _adminRepository.UpdateCorporateMasterStatus(corporateId, isActive, globalValues);

            if (serviceResult.Result)
                _log.Info($"Corporate status updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Corporate status update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDiscountApprovalMaster")]
        [Authorize]
        public IActionResult CreateUpdateDiscountApprovalMaster([FromBody] DiscountApprovalMasterRequest request)
        {
            _log.Info($"CreateUpdateDiscountApprovalMaster called. Name={request.DiscountApprovalName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for discount approval insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateDiscountApprovalMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Discount approval operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Discount approval operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDiscountApprovalMasterList")]
        [Authorize]
        public IActionResult GetDiscountApprovalMasterList(
            [FromQuery] string name = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetDiscountApprovalMasterList called. Name={name ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _adminRepository.GetDiscountApprovalMasterList(name, isActive);

            if (serviceResult.Result)
                _log.Info($"Discount approval list fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Discount approval list fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveUserwiseDiscountMaster")]
        [Authorize]
        public IActionResult SaveUserwiseDiscountMaster([FromBody] List<UserwiseDiscountMasterRequest> request)
        {
            _log.Info($"SaveUserwiseDiscountMaster called. Records Count={request?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUserwiseDiscountMaster.");
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
            var serviceResult = _adminRepository.SaveUserwiseDiscountMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveUserwiseDiscountMaster completed: {serviceResult.Message}");
            else
                _log.Warn($"SaveUserwiseDiscountMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserwiseDiscountMaster")]
        [Authorize]
        public IActionResult GetUserwiseDiscountMaster()
        {
            _log.Info("GetUserwiseDiscountMaster called.");

            var serviceResult = _adminRepository.GetUserwiseDiscountMaster();

            if (serviceResult.Result)
                _log.Info($"GetUserwiseDiscountMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetUserwiseDiscountMaster failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateDoctorHeader")]
        [Authorize]
        public IActionResult CreateUpdateDoctorHeader([FromBody] CreateUpdateDoctorHeaderRequest request)
        {
            _log.Info($"CreateUpdateDoctorHeader called. HeaderId={request.HeaderId}, HeaderName={request.HeaderName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for doctor header insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // IsActive must be 0 or 1
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
            var serviceResult = _adminRepository.CreateUpdateDoctorHeader(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor header operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Doctor header operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get all Doctor Header Masters.
        /// Optionally filter by headerId (in-memory from Redis cache).
        /// </summary>
        [HttpGet("getAllDoctorHeaderMaster")]
        [Authorize]
        public IActionResult GetAllDoctorHeaderMaster([FromQuery] int? headerId = null)
        {
            _log.Info($"GetAllDoctorHeaderMaster called. HeaderId={headerId?.ToString() ?? "All"}");

            if (headerId.HasValue && headerId.Value <= 0)
            {
                _log.Warn("Invalid HeaderId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "HeaderId must be greater than 0",
                    errors = new { headerId }
                });
            }

            var serviceResult = _adminRepository.GetAllDoctorHeaderMaster(headerId);

            if (serviceResult.Result)
                _log.Info($"Doctor headers fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor headers fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get LOV values for a specific Doctor Header.
        /// </summary>
        [HttpGet("getDoctorHeaderLOVs")]
        [Authorize]
        public IActionResult GetDoctorHeaderLOVs([FromQuery] int headerId)
        {
            _log.Info($"GetDoctorHeaderLOVs called. HeaderId={headerId}");

            if (headerId <= 0)
            {
                _log.Warn("Invalid HeaderId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "HeaderId must be greater than 0",
                    errors = new { headerId }
                });
            }

            var serviceResult = _adminRepository.GetDoctorHeaderLOVs(headerId);

            if (serviceResult.Result)
                _log.Info($"Doctor header LOVs fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor header LOVs fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get all active header masters with mapping status for a type/relatedTo combination.
        /// </summary>
        [HttpGet("getDoctorHeaderMappingForMaster")]
        [Authorize]
        public IActionResult GetDoctorHeaderMappingForMaster(
            [FromQuery] int typeId,
            [FromQuery] int relatedToId)
        {
            _log.Info($"GetDoctorHeaderMappingForMaster called. TypeId={typeId}, RelatedToId={relatedToId}");

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

            if (relatedToId <= 0)
            {
                _log.Warn("Invalid RelatedToId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RelatedToId must be greater than 0",
                    errors = new { relatedToId }
                });
            }

            var serviceResult = _adminRepository.GetDoctorHeaderMappingForMaster(typeId, relatedToId);

            if (serviceResult.Result)
                _log.Info($"Doctor header mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor header mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Save (replace) Doctor Header Department Mapping for a given type/relatedTo.
        /// </summary>
        [HttpPost("saveDoctorHeaderDepartmentMapping")]
        [Authorize]
        public IActionResult SaveDoctorHeaderDepartmentMapping([FromBody] SaveDoctorHeaderMappingRequest request)
        {
            _log.Info($"SaveDoctorHeaderDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for save doctor header department mapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.TypeId <= 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId = request.TypeId }
                });
            }

            if (request.RelatedToId <= 0)
            {
                _log.Warn("Invalid RelatedToId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RelatedToId must be greater than 0",
                    errors = new { relatedToId = request.RelatedToId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveDoctorHeaderDepartmentMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Doctor header department mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Doctor header department mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateServiceItemMaster")]
        [Authorize]
        public IActionResult CreateUpdateServiceItemMaster([FromBody] CreateUpdateServiceItemMasterRequest request)
        {
            _log.Info($"CreateUpdateServiceItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for service item insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateServiceItemMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Service item operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Service item operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdatePrintGroupMaster")]
        [Authorize]
        public IActionResult CreateUpdatePrintGroupMaster([FromBody] CreateUpdatePrintGroupMasterRequest request)
        {
            _log.Info($"CreateUpdatePrintGroupMaster called. PrintGroupId={request.PrintGroupId}, PrintGroupName={request.PrintGroupName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for PrintGroupMaster insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdatePrintGroupMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"PrintGroupMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"PrintGroupMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPrintGroupMaster")]
        [Authorize]
        public IActionResult GetPrintGroupMaster([FromQuery] int? printGroupId = null)
        {
            _log.Info($"GetPrintGroupMaster called. PrintGroupId={printGroupId?.ToString() ?? "All"}");

            if (printGroupId.HasValue && printGroupId.Value <= 0)
            {
                _log.Warn("Invalid PrintGroupId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PrintGroupId must be greater than 0",
                    errors = new { printGroupId }
                });
            }

            var serviceResult = _adminRepository.GetPrintGroupMaster(printGroupId);

            if (serviceResult.Result)
                _log.Info($"PrintGroupMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PrintGroupMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateWardNameMaster")]
        [Authorize]
        public IActionResult CreateUpdateWardNameMaster([FromBody] CreateUpdateWardNameMasterRequest request)
        {
            _log.Info($"CreateUpdateWardNameMaster called. WardNameId={request.WardNameId}, WardName={request.WardName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for WardNameMaster insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateWardNameMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"WardNameMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"WardNameMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getWardNameMaster")]
        [Authorize]
        public IActionResult GetWardNameMaster([FromQuery] int? wardNameId = null)
        {
            _log.Info($"GetWardNameMaster called. WardNameId={wardNameId?.ToString() ?? "All"}");

            if (wardNameId.HasValue && wardNameId.Value <= 0)
            {
                _log.Warn("Invalid WardNameId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "WardNameId must be greater than 0",
                    errors = new { wardNameId }
                });
            }

            var serviceResult = _adminRepository.GetWardNameMaster(wardNameId);

            if (serviceResult.Result)
                _log.Info($"WardNameMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"WardNameMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateBlockMaster")]
        [Authorize]
        public IActionResult CreateUpdateBlockMaster([FromBody] CreateUpdateBlockMasterRequest request)
        {
            _log.Info($"CreateUpdateBlockMaster called. BlockId={request.BlockId}, BlockName={request.BlockName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for Block insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBlockMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Block operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Block operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get Block List. If BlockId is null, returns all Blocks; otherwise returns the matching Block.
        /// </summary>
        [HttpGet("getBlockList")]
        [Authorize]
        public IActionResult GetBlockList([FromQuery] int? BlockId = null)
        {
            _log.Info($"GetBlockList called. BlockId={BlockId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.GetBlockList(BlockId);

            if (serviceResult.Result)
                _log.Info($"Blocks fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No Blocks found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateFloorMaster")]
        [Authorize]
        public IActionResult CreateUpdateFloorMaster([FromBody] CreateUpdateFloorMasterRequest request)
        {
            _log.Info($"CreateUpdateFloorMaster called. FloorId={request.FloorId}, FloorName={request.FloorName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for floor insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateFloorMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Floor operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Floor operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>
        /// Get Floor List. If floorId is null, returns all floors; otherwise returns the matching floor.
        /// </summary>
        [HttpGet("getFloorList")]
        [Authorize]
        public IActionResult GetFloorList([FromQuery] int? floorId = null)
        {
            _log.Info($"GetFloorList called. FloorId={floorId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.GetFloorList(floorId);

            if (serviceResult.Result)
                _log.Info($"Floors fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No floors found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateBedMaster")]
        [Authorize]
        public IActionResult CreateUpdateBedMaster([FromBody] CreateUpdateBedMasterRequest request)
        {
            _log.Info($"CreateUpdateBedMaster called. BedId={request.BedId}, BranchId={request.BranchId}, WardNameId={request.WardNameId}, BedNo={request.BedNo}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for bed insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateBedMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Bed operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Bed operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getAllBedList")]
        [Authorize]
        public IActionResult GetAllBedList(
     [FromQuery] int? branchId = null,
          [FromQuery] int? typeId = null,
     [FromQuery] int? blockId = null,
     [FromQuery] int? floorId = null,
     [FromQuery] int? wardNameId = null,
     [FromQuery] int? bedId = null,
     [FromQuery] int? isActive = null
    )
        {
            _log.Info($"GetAllBedList called. BedId={bedId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}, FloorId={floorId?.ToString() ?? "All"}, WardNameId={wardNameId?.ToString() ?? "All"}, BranchId={branchId?.ToString() ?? "All"}, TypeId={typeId?.ToString() ?? "All"}");

            if (bedId.HasValue && bedId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BedId must be greater than 0", errors = new { bedId } });
            }
            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "IsActive must be 0 (Inactive), 1 (Active), or null (All)", errors = new { isActive } });
            }
            if (blockId.HasValue && blockId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BlockId must be greater than 0", errors = new { blockId } });
            }
            if (floorId.HasValue && floorId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "FloorId must be greater than 0", errors = new { floorId } });
            }
            if (wardNameId.HasValue && wardNameId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "WardNameId must be greater than 0", errors = new { wardNameId } });
            }
            if (branchId.HasValue && branchId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }
            if (typeId.HasValue && typeId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "TypeId must be greater than 0", errors = new { typeId } });
            }

            var serviceResult = _adminRepository.GetAllBedList(bedId, isActive, blockId,floorId, wardNameId, branchId, typeId);

            if (serviceResult.Result)
                _log.Info($"BedMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"BedMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("createUpdateTabGroupTypeMaster")]
        [Authorize]
        public IActionResult CreateUpdateTabGroupTypeMaster([FromBody] CreateUpdateTabGroupTypeMasterRequest request)
        {
            _log.Info($"CreateUpdateTabGroupTypeMaster called. GroupTypeId={request.GroupTypeId}, GroupTypeName={request.GroupTypeName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for TabGroupTypeMaster insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateTabGroupTypeMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"TabGroupTypeMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"TabGroupTypeMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getTabGroupTypeMaster")]
        [Authorize]
        public IActionResult GetTabGroupTypeMaster(
            [FromQuery] int? groupTypeId = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetTabGroupTypeMaster called. GroupTypeId={groupTypeId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

            if (groupTypeId.HasValue && groupTypeId.Value <= 0)
            {
                _log.Warn("Invalid GroupTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "GroupTypeId must be greater than 0",
                    errors = new { groupTypeId }
                });
            }

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

            var serviceResult = _adminRepository.GetTabGroupTypeMaster(groupTypeId, isActive);

            if (serviceResult.Result)
                _log.Info($"TabGroupTypeMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"TabGroupTypeMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateIPDTabMaster")]
        [Authorize]
        public IActionResult CreateUpdateIPDTabMaster([FromBody] CreateUpdateIPDTabMasterRequest request)
        {
            _log.Info($"CreateUpdateIPDTabMaster called. TabId={request.TabId}, TabName={request.TabName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for IPDTabMaster insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateIPDTabMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPDTabMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"IPDTabMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDTabMaster")]
        [Authorize]
        public IActionResult GetIPDTabMaster(
            [FromQuery] int? tabId = null,
            [FromQuery] int? groupTypeId = null,
            [FromQuery] int? tabTypeId = null,
            [FromQuery] int? roomTypeId = null,
   [FromQuery] string tabName = null,
            [FromQuery] int? isActive = null)
        {
            _log.Info($"GetIPDTabMaster called. TabId={tabId?.ToString() ?? "All"}, GroupTypeId={groupTypeId?.ToString() ?? "All"}, TabTypeId={tabTypeId?.ToString() ?? "All"}, RoomTypeId={roomTypeId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

            if (tabId.HasValue && tabId.Value <= 0)
            {
                _log.Warn("Invalid TabId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TabId must be greater than 0",
                    errors = new { tabId }
                });
            }

            if (groupTypeId.HasValue && groupTypeId.Value <= 0)
            {
                _log.Warn("Invalid GroupTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "GroupTypeId must be greater than 0",
                    errors = new { groupTypeId }
                });
            }

            if (tabTypeId.HasValue && tabTypeId.Value <= 0)
            {
                _log.Warn("Invalid TabTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TabTypeId must be greater than 0",
                    errors = new { tabTypeId }
                });
            }

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

            var serviceResult = _adminRepository.GetIPDTabMaster(tabId, groupTypeId, tabTypeId, roomTypeId, tabName, isActive);

            if (serviceResult.Result)
                _log.Info($"IPDTabMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPDTabMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveUpdateRoleWiseIPDTabMapping")]
        [Authorize]
        public IActionResult SaveUpdateRoleWiseIPDTabMapping([FromBody] SaveRoleWiseIPDTabMappingRequest request)
        {
            _log.Info($"SaveUpdateRoleWiseIPDTabMapping called. RoleId={request.RoleId}, TabMappings Count={request.TabMappings.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateRoleWiseIPDTabMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveUpdateRoleWiseIPDTabMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Role-wise IPD tab mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"Role-wise IPD tab mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getRoleWiseIPDTabListMaster")]
        [Authorize]
        public IActionResult GetRoleWiseIPDTabListMaster([FromQuery] int roleId)
        {
            _log.Info($"GetRoleWiseIPDTabListMaster called. RoleId={roleId}");

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

            var serviceResult = _adminRepository.GetRoleWiseIPDTabListMaster(roleId);

            if (serviceResult.Result)
                _log.Info($"Role-wise IPD tab list fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Role-wise IPD tab list fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("saveUpdateUserIPDTabMapping")]
        [Authorize]
        public IActionResult SaveUpdateUserIPDTabMapping([FromBody] SaveUserIPDTabMappingRequest request)
        {
            _log.Info($"SaveUpdateUserIPDTabMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TabMappings Count={request.TabMappings.Count}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserIPDTabMapping.");
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

            // Validate TypeId
            if (request.TypeId <= 0)
            {
                _log.Warn("Invalid TypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than 0",
                    errors = new { typeId = request.TypeId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveUpdateUserIPDTabMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD tab mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD tab mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserGrantedRemainingTabMaster")]
        [Authorize]
        public IActionResult GetUserGrantedRemainingTabMaster(
            [FromQuery] int branchId,
            [FromQuery] int typeId,
            [FromQuery] int userId,
            [FromQuery] int roleId)
        {
            _log.Info($"GetUserGrantedRemainingTabMaster called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0 || roleId <= 0)
            {
                _log.Warn("Invalid parameters for GetUserGrantedRemainingTabMaster.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId, roleId) must be greater than 0",
                    errors = new { branchId, typeId, userId, roleId }
                });
            }

            var serviceResult = _adminRepository.GetUserGrantedRemainingTabMaster(branchId, typeId, userId, roleId);

            if (serviceResult.Result)
                _log.Info($"IPD tab mapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD tab mapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateApprovalAuthorityMaster")]
        [Authorize]
        public IActionResult CreateUpdateApprovalAuthorityMaster(
            [FromBody] CreateUpdateApprovalAuthorityMasterRequest request)
        {
            _log.Info($"CreateUpdateApprovalAuthorityMaster called. Id={request.Id}, ApprovalTypeId={request.ApprovalTypeId}, BranchId={request.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApprovalAuthorityMaster insert/update.");
                var validationAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = validationAlert.Type,
                    message = validationAlert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateApprovalAuthorityMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApprovalAuthorityMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"ApprovalAuthorityMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getApprovalAuthorityMasterList")]
        [Authorize]
        public IActionResult GetApprovalAuthorityMasterList([FromQuery] int approvalTypeId)
        {
            _log.Info($"GetApprovalAuthorityMasterList called. ApprovalTypeId={approvalTypeId}");

            if (approvalTypeId <= 0)
            {
                _log.Warn("Invalid ApprovalTypeId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ApprovalTypeId must be greater than 0",
                    errors = new { approvalTypeId }
                });
            }

            var serviceResult = _adminRepository.GetApprovalAuthorityMasterList(approvalTypeId);

            if (serviceResult.Result)
                _log.Info($"ApprovalAuthorityMaster list fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"ApprovalAuthorityMaster list fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateApprovalAuthorityMasterStatus")]
        [Authorize]
        public IActionResult UpdateApprovalAuthorityMasterStatus([FromQuery] int id)
        {
            _log.Info($"UpdateApprovalAuthorityMasterStatus called. Id={id}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided for ApprovalAuthorityMaster status toggle.");
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
            var serviceResult = _adminRepository.UpdateApprovalAuthorityMasterStatus(id, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApprovalAuthorityMaster status toggled successfully: {serviceResult.Message}");
            else
                _log.Warn($"ApprovalAuthorityMaster status toggle failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #region Branch Corporate Ratelist Mapping

        [HttpPost("saveBranchCorporateRatelistMapping")]
        [Authorize]
        public IActionResult SaveBranchCorporateRatelistMapping([FromBody] SaveBranchCorporateRatelistMappingRequest request)
        {
            _log.Info($"SaveBranchCorporateRatelistMapping called. BranchId={request.BranchId}, CorporateId={request.CorporateId}, Count={request.Mappings?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveBranchCorporateRatelistMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate all ServiceItemIds > 0 if any provided
            if (request.Mappings != null && request.Mappings.Any())
            {
                var invalid = request.Mappings
                    .Where(m => string.IsNullOrWhiteSpace(m.RateListIdOPD) || string.IsNullOrWhiteSpace(m.RateListIdIPD))
                    .ToList();

                if (invalid.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All mapping items must have valid RateListIdOPD and RateListIdIPD values"
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveBranchCorporateRatelistMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveBranchCorporateRatelistMapping completed: {serviceResult.Message}");
            else
                _log.Warn($"SaveBranchCorporateRatelistMapping failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBranchCorporateRatelistMapping")]
        [Authorize]
        public IActionResult GetBranchCorporateRatelistMapping(
      [FromQuery] int? branchId = null,
      [FromQuery] int? corporateId = null)
        {
            _log.Info($"GetBranchCorporateRatelistMapping called. BranchId={branchId?.ToString() ?? "All"}, CorporateId={corporateId?.ToString() ?? "All"}");

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

            if (corporateId.HasValue && corporateId.Value <= 0)
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

            var serviceResult = _adminRepository.GetBranchCorporateRatelistMapping(branchId, corporateId);

            if (serviceResult.Result)
                _log.Info($"BranchCorporateRatelistMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"BranchCorporateRatelistMapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Branch Corporate Wise Service Exclusion Mapping

        [HttpPost("saveBranchCorporateServiceExclusionMapping")]
        [Authorize]
        public IActionResult SaveBranchCorporateServiceExclusionMapping([FromBody] SaveBranchCorporateServiceExclusionRequest request)
        {
            _log.Info($"SaveBranchCorporateServiceExclusionMapping called. BranchId={request.BranchId}, CorporateId={request.CorporateId}, Count={request.ServiceItemIds?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveBranchCorporateServiceExclusionMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate all ServiceItemIds > 0 if any provided
            if (request.ServiceItemIds != null && request.ServiceItemIds.Any())
            {
                var invalidIds = request.ServiceItemIds.Where(id => id <= 0).ToList();
                if (invalidIds.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All ServiceItemIds must be greater than 0",
                        errors = new { invalidIds }
                    });
                }

                var duplicateIds = request.ServiceItemIds
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateIds.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Duplicate ServiceItemIds are not allowed",
                        errors = new { duplicateIds }
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveBranchCorporateServiceExclusionMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveBranchCorporateServiceExclusionMapping completed: {serviceResult.Message}");
            else
                _log.Warn($"SaveBranchCorporateServiceExclusionMapping failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBranchCorporateServiceExclusionMapping")]
        [Authorize]
        public IActionResult GetBranchCorporateServiceExclusionMapping(
     [FromQuery] int? branchId = null,
     [FromQuery] int? corporateId = null)
        {
            _log.Info($"GetBranchCorporateServiceExclusionMapping called. BranchId={branchId?.ToString() ?? "All"}, CorporateId={corporateId?.ToString() ?? "All"}");

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

            if (corporateId.HasValue && corporateId.Value <= 0)
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

            var serviceResult = _adminRepository.GetBranchCorporateServiceExclusionMapping(branchId, corporateId);

            if (serviceResult.Result)
                _log.Info($"BranchCorporateServiceExclusionMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"BranchCorporateServiceExclusionMapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        #endregion

        #region Branch Right Mapping

        [HttpPost("saveBranchRightMapping")]
        [Authorize]
        public IActionResult SaveBranchRightMapping([FromBody] SaveBranchRightMappingRequest request)
        {
            _log.Info($"SaveBranchRightMapping called. BranchId={request.BranchId}, RightCount={request.BranchRightIds?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveBranchRightMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.BranchRightIds != null && request.BranchRightIds.Any())
            {
                var invalidIds = request.BranchRightIds.Where(id => id <= 0).ToList();
                if (invalidIds.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All BranchRightIds must be greater than 0",
                        errors = new { invalidIds }
                    });
                }

                var duplicateIds = request.BranchRightIds
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateIds.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Duplicate BranchRightIds are not allowed",
                        errors = new { duplicateIds }
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveBranchRightMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveBranchRightMapping completed: {serviceResult.Message}");
            else
                _log.Warn($"SaveBranchRightMapping failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBranchRightMapping")]
        [Authorize]
        public IActionResult GetBranchRightMapping([FromQuery] int branchId)
        {
            _log.Info("GetBranchRightMapping called.");
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

            var serviceResult = _adminRepository.GetBranchRightMapping(branchId);

            if (serviceResult.Result)
                _log.Info($"BranchRightMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"BranchRightMapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpPatch("updateDefaultBranchSetting")]
        [Authorize]
        public IActionResult UpdateDefaultBranchSetting([FromBody] UpdateDefaultBranchSettingRequest request)
        {
            _log.Info($"UpdateDefaultBranchSetting called. BranchId={request.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateDefaultBranchSetting.");
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
            var serviceResult = _adminRepository.UpdateDefaultBranchSetting(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Default branch settings updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Default branch settings update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        #endregion



        [HttpPost("createUpdateVitalMaster")]
        [Authorize]
        public IActionResult CreateUpdateVitalMaster([FromBody] CreateUpdateVitalMasterRequest request)
        {
            _log.Info($"CreateUpdateVitalMaster called. VitalId={request.VitalId}, VitalName={request.VitalName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for vital master insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateVitalMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"VitalMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"VitalMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getVitalMasterList")]
        [Authorize]
        public IActionResult GetVitalMasterList([FromQuery] int? isActive = null)
        {
            _log.Info($"GetVitalMasterList called. IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _adminRepository.GetVitalMasterList(isActive);

            if (serviceResult.Result)
                _log.Info($"VitalMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"VitalMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateVitalUnitMaster")]
        [Authorize]
        public IActionResult CreateUpdateVitalUnitMaster([FromBody] CreateUpdateVitalUnitMasterRequest request)
        {
            _log.Info($"CreateUpdateVitalUnitMaster called. Id={request.Id}, UnitName={request.UnitName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for vital unit master insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateVitalUnitMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"VitalUnitMaster operation completed: {serviceResult.Message}");
            else
                _log.Warn($"VitalUnitMaster operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getVitalUnitMasterList")]
        [Authorize]
        public IActionResult GetVitalUnitMasterList()
        {
            _log.Info("GetVitalUnitMasterList called.");

            var serviceResult = _adminRepository.GetVitalUnitMasterList();

            if (serviceResult.Result)
                _log.Info($"VitalUnitMaster fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"VitalUnitMaster fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getVitalDepartmentMapping")]
        [Authorize]
        public IActionResult GetVitalDepartmentMapping(
    [FromQuery] int typeId,
    [FromQuery] int relatedToId)
        {
            _log.Info($"GetVitalDepartmentMapping called. TypeId={typeId}, RelatedToId={relatedToId}");

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

            if (relatedToId <= 0)
            {
                _log.Warn("Invalid RelatedToId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RelatedToId must be greater than 0",
                    errors = new { relatedToId }
                });
            }

            var serviceResult = _adminRepository.GetVitalDepartmentMapping(typeId, relatedToId);

            if (serviceResult.Result)
                _log.Info($"VitalDepartmentMapping fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"VitalDepartmentMapping fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveVitalDepartmentMapping")]
        [Authorize]
        public IActionResult SaveVitalDepartmentMapping([FromBody] SaveVitalDepartmentMappingRequest request)
        {
            _log.Info($"SaveVitalDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}, Items={request.HeaderMappingData?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveVitalDepartmentMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.HeaderMappingData != null && request.HeaderMappingData.Any())
            {
                var invalidRows = request.HeaderMappingData
                    .Where(x => x.vitalId <= 0 )
                    .ToList();

                if (invalidRows.Any())
                {
                    _log.Warn($"{invalidRows.Count} row(s) have invalid TypeId, VitalId, or RelatedToId.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Every mapping row must have VitalId > 0 "
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveVitalDepartmentMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveVitalDepartmentMapping completed: {serviceResult.Message}");
            else
                _log.Warn($"SaveVitalDepartmentMapping failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdatePackageMaster")]
        [Authorize]
        public IActionResult CreateUpdatePackageMaster([FromBody] CreateUpdatePackageMasterRequest request)
        {
            _log.Info($"CreateUpdatePackageMaster called. PackageId={request?.PackageId}, Name={request?.Name}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CreateUpdatePackageMaster.");
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
            var serviceResult = _adminRepository.CreateUpdatePackageMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Package master operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Package master operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateNavigationSubMenuSequenceNo")]
        [Authorize]
        public IActionResult UpdateNavigationSubMenuSequenceNo([FromBody] UpdateNavigationSubMenuSequenceRequest request)
        {
            _log.Info($"UpdateNavigationSubMenuSequenceNo called. Count={request?.SubMenus?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateNavigationSubMenuSequenceNo.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.SubMenus == null || !request.SubMenus.Any())
            {
                _log.Warn("No submenu sequence items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one submenu sequence is required",
                    errors = new[] { "SubMenus cannot be empty" }
                });
            }

            var invalidItems = request.SubMenus.Where(x => x.SubMenuId <= 0).ToList();
            if (invalidItems.Any())
            {
                _log.Warn("Invalid SubMenuId(s) provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All SubMenuIds must be greater than 0",
                    errors = new { invalidSubMenuIds = invalidItems.Select(x => x.SubMenuId).ToList() }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.UpdateNavigationSubMenuSequenceNo(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Sub menu sequence updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Sub menu sequence update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateNavigationTabSequenceNo")]
        [Authorize]
        public IActionResult UpdateNavigationTabSequenceNo([FromBody] UpdateNavigationTabSequenceRequest request)
        {
            _log.Info($"UpdateNavigationTabSequenceNo called. Count={request?.Tabs?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateNavigationTabSequenceNo.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.Tabs == null || !request.Tabs.Any())
            {
                _log.Warn("No tab sequence items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one tab sequence is required",
                    errors = new[] { "Tabs cannot be empty" }
                });
            }

            var invalidItems = request.Tabs.Where(x => x.TabId <= 0).ToList();
            if (invalidItems.Any())
            {
                _log.Warn("Invalid TabId(s) provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All TabIds must be greater than 0",
                    errors = new { invalidTabIds = invalidItems.Select(x => x.TabId).ToList() }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.UpdateNavigationTabSequenceNo(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Tab sequence updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"Tab sequence update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateSurgeryComponentMaster")]
        [Authorize]
        public IActionResult CreateUpdateSurgeryComponentMaster([FromBody] CreateUpdateSurgeryComponentMasterRequest request)
        {
            _log.Info($"CreateUpdateSurgeryComponentMaster called. ComponentId={request.ComponentId}, ComponentName={request.ComponentName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for surgery component insert/update.");
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
            var serviceResult = _adminRepository.CreateUpdateSurgeryComponentMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SurgeryComponent operation completed: {serviceResult.Message}");
            else
                _log.Warn($"SurgeryComponent operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getSurgeryComponentsList")]
        [Authorize]
        public IActionResult GetSurgeryComponentsList([FromQuery] int? isActive = null)
        {
            _log.Info($"GetSurgeryComponentsList called. IsActive={isActive?.ToString() ?? "All"}");

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

            var serviceResult = _adminRepository.GetSurgeryComponentsList(isActive);

            if (serviceResult.Result)
                _log.Info($"SurgeryComponents fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"SurgeryComponents fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDischargeProcessMaster")]
        [Authorize]
        public IActionResult GetDischargeProcessMaster([FromQuery] int? isActive = null)
        {
            _log.Info($"GetDischargeProcessMaster called. IsActive={isActive?.ToString() ?? "All"}");

            if (isActive.HasValue && isActive.Value != 0 && isActive.Value != 1)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "IsActive must be 0, 1, or null (All)", errors = new { isActive } });
            }

            var serviceResult = _adminRepository.GetDischargeProcessMaster(isActive);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDischargeProcessMasterById")]
        [Authorize]
        public IActionResult GetDischargeProcessMasterById([FromQuery] int dischargeProcessId)
        {
            _log.Info($"GetDischargeProcessMasterById called. DischargeProcessId={dischargeProcessId}");

            if (dischargeProcessId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "DischargeProcessId must be greater than 0", errors = new { dischargeProcessId } });
            }

            var serviceResult = _adminRepository.GetDischargeProcessMasterById(dischargeProcessId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("createUpdateDischargeProcessMaster")]
        [Authorize]
        public IActionResult CreateUpdateDischargeProcessMaster([FromBody] CreateUpdateDischargeProcessMasterRequest request)
        {
            _log.Info($"CreateUpdateDischargeProcessMaster called. DischargeProcessId={request.DischargeProcessId}, ProcessKey={request.ProcessKey}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.CreateUpdateDischargeProcessMaster(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateDischargeProcessSequence")]
        [Authorize]
        public IActionResult UpdateDischargeProcessSequence([FromBody] UpdateDischargeProcessSequenceRequest request)
        {
            _log.Info($"UpdateDischargeProcessSequence called. Count={request?.Sequences?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var duplicateSeq = request.Sequences.GroupBy(s => s.SequenceNo).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateSeq.Any())
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "Duplicate SequenceNo values are not allowed", errors = new { duplicateSeq } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.UpdateDischargeProcessSequence(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─── Corporate Mapping ─────────────────────────────────────────────

        [HttpPost("saveDischargeProcessCorporateMapping")]
        [Authorize]
        public IActionResult SaveDischargeProcessCorporateMapping([FromBody] SaveDischargeProcessCorporateMappingRequest request)
        {
            _log.Info($"SaveDischargeProcessCorporateMapping called. DischargeProcessId={request.DischargeProcessId}, Count={request?.CorporateIds?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            if (request.CorporateIds != null && request.CorporateIds.Any(id => id <= 0))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "All CorporateIds must be greater than 0" });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveDischargeProcessCorporateMapping(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDischargeProcessCorporateMapping")]
        [Authorize]
        public IActionResult GetDischargeProcessCorporateMapping([FromQuery] int? dischargeProcessId = null)
        {
            _log.Info($"GetDischargeProcessCorporateMapping called. DischargeProcessId={dischargeProcessId?.ToString() ?? "All"}");

            var serviceResult = _adminRepository.GetDischargeProcessCorporateMapping(dischargeProcessId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveUpdateUserDischargeProcessMapping")]
        [Authorize]
        public IActionResult SaveUpdateUserDischargeProcessMapping([FromBody] SaveUserDischargeProcessMappingRequest request)
        {
            _log.Info($"SaveUpdateUserDischargeProcessMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, IsFirst={request.IsFirst}, Mappings Count={request.UserDischargeProcessMappings?.Count ?? 0}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveUpdateUserDischargeProcessMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsFirst != 0 && request.IsFirst != 1)
            {
                _log.Warn($"Invalid IsFirst parameter: {request.IsFirst}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsFirst must be either 0 or 1",
                    errors = new { IsFirst = request.IsFirst }
                });
            }

            if (request.UserDischargeProcessMappings != null && request.UserDischargeProcessMappings.Count > 0)
            {
                bool isConsistent = request.UserDischargeProcessMappings.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    _log.Warn("Inconsistent typeId, userId, or branchId in user discharge process mapping list.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All user discharge process mapping items must have the same typeId, userId, and branchId as the request"
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _adminRepository.SaveUpdateUserDischargeProcessMapping(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"User discharge process mapping saved successfully: {serviceResult.Message}");
            else
                _log.Warn($"User discharge process mapping save failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserWiseDischargeProcessMapping")]
        [Authorize]
        public IActionResult GetUserWiseDischargeProcessMapping(
            [FromQuery] int branchId,
            [FromQuery] int typeId,
            [FromQuery] int userId)
        {
            _log.Info($"GetUserWiseDischargeProcessMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

            if (branchId <= 0 || typeId <= 0 || userId <= 0)
            {
                _log.Warn("Invalid parameters for GetUserWiseDischargeProcessMapping.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All parameters (branchId, typeId, userId) must be greater than 0",
                    errors = new { branchId, typeId, userId }
                });
            }

            var serviceResult = _adminRepository.GetUserWiseDischargeProcessMapping(branchId, typeId, userId);

            if (serviceResult.Result)
                _log.Info($"User discharge process mapping (granted + remaining) fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"User discharge process mapping fetch failed: {serviceResult.Message}");

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
