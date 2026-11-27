using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Lab Approval Authority Master (owner 2026-11-27): who may approve lab reports, per branch (0 = default),
    /// lab type and sub departments. A user sees and saves only the lab types they hold the setup right for
    /// (CanSetupLabApprovalAuthorityFor...), checked in the procedures. Nothing is cached: the data depends on
    /// the user's rights. SPs: Database/2026-11-27_LabApprovalAuthorityMaster.sql.
    /// </summary>
    public partial class LabRepository
    {
        public ServiceResult<LabApprovalAuthorityLookupsModel> GetLabApprovalAuthorityLookups(
            int sessionBranchId, int roleId, AllGlobalValues globalValues)
        {
            try
            {
                var set = _sqlHelper.GetDataSet("S_GetLabApprovalAuthorityLookups", CommandType.StoredProcedure,
                    new { userId = globalValues.userId, sessionBranchId, roleId });

                var data = new LabApprovalAuthorityLookupsModel
                {
                    LabTypes = set.Tables[0].AsEnumerable().Select(r => new LabApprovalLabTypeModel
                    {
                        LabTypeId = Convert.ToInt32(r["LabTypeId"]),
                        LabTypeName = r["LabTypeName"] as string ?? ""
                    }).ToList(),
                    SubDepartments = set.Tables[1].AsEnumerable().Select(r => new LabApprovalSubDepartmentModel
                    {
                        SubSubCategoryId = Convert.ToInt32(r["SubSubCategoryId"]),
                        SubSubCategoryName = r["SubSubCategoryName"] as string ?? "",
                        LabTypeId = Convert.ToInt32(r["LabTypeId"])
                    }).ToList(),
                    Doctors = set.Tables[2].AsEnumerable().Select(r => new LabApprovalDoctorModel
                    {
                        DoctorId = Convert.ToInt32(r["DoctorId"]),
                        DoctorName = r["DoctorName"] as string ?? ""
                    }).ToList(),
                    ProvisionalUsers = set.Tables[3].AsEnumerable().Select(r => new LabApprovalUserModel
                    {
                        UserId = Convert.ToInt32(r["UserId"]),
                        UserName = r["UserName"] as string ?? ""
                    }).ToList()
                };

                return ServiceResult<LabApprovalAuthorityLookupsModel>.Success(data, "Info", "Data retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return LabApprovalError<LabApprovalAuthorityLookupsModel>(ex, nameof(GetLabApprovalAuthorityLookups));
            }
        }

        public ServiceResult<IEnumerable<LabApprovalAuthorityModel>> GetLabApprovalAuthorityList(
            int sessionBranchId, int roleId, AllGlobalValues globalValues)
        {
            try
            {
                var table = _sqlHelper.GetDataTable("S_GetLabApprovalAuthorityList", CommandType.StoredProcedure,
                    new { userId = globalValues.userId, sessionBranchId, roleId });

                static string? Text(DataRow r, string column) => r[column] as string;
                static int Number(DataRow r, string column) => Convert.ToInt32(r[column]);

                var list = table.AsEnumerable().Select(r => new LabApprovalAuthorityModel
                {
                    LabApprovalAuthorityId = Number(r, "LabApprovalAuthorityId"),
                    BranchId = Number(r, "BranchId"),
                    BranchName = Text(r, "BranchName"),
                    LabTypeId = Number(r, "LabTypeId"),
                    LabTypeName = Text(r, "LabTypeName"),
                    SubSubCategoryIds = Text(r, "SubSubCategoryIds"),
                    SubDepartmentNames = Text(r, "SubDepartmentNames"),
                    ApprovalFlowId = Number(r, "ApprovalFlowId"),
                    ApprovalFlow = Text(r, "ApprovalFlow"),
                    IsAllApprovalRequired = Number(r, "IsAllApprovalRequired"),
                    ApprovalLevelId = Number(r, "ApprovalLevelId"),
                    ApprovalLevel = Text(r, "ApprovalLevel"),
                    Level1DoctorIds = Text(r, "Level1DoctorIds"),
                    Level2DoctorIds = Text(r, "Level2DoctorIds"),
                    Level3DoctorIds = Text(r, "Level3DoctorIds"),
                    Level4DoctorIds = Text(r, "Level4DoctorIds"),
                    Level1DoctorNames = Text(r, "Level1DoctorNames"),
                    Level2DoctorNames = Text(r, "Level2DoctorNames"),
                    Level3DoctorNames = Text(r, "Level3DoctorNames"),
                    Level4DoctorNames = Text(r, "Level4DoctorNames"),
                    ProvisionalUserIds = Text(r, "ProvisionalUserIds"),
                    ProvisionalUserNames = Text(r, "ProvisionalUserNames"),
                    IsActive = Number(r, "IsActive"),
                    CreatedBy = Text(r, "CreatedBy"),
                    CreatedOn = Text(r, "CreatedOn"),
                    LastModifiedBy = Text(r, "LastModifiedBy"),
                    LastModifiedOn = Text(r, "LastModifiedOn")
                }).ToList();

                return ServiceResult<IEnumerable<LabApprovalAuthorityModel>>.Success(
                    list, "Info", $"{list.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return LabApprovalError<IEnumerable<LabApprovalAuthorityModel>>(ex, nameof(GetLabApprovalAuthorityList));
            }
        }

        public ServiceResult<LabApprovalAuthorityResponse> CreateUpdateLabApprovalAuthority(
            CreateUpdateLabApprovalAuthorityRequest request, AllGlobalValues globalValues)
        {
            try
            {
                int level = request.ApprovalLevelId;

                // only the doctors of the chosen levels are kept; each of those levels needs one
                var levelDoctors = new[]
                {
                    request.Level1DoctorIds, request.Level2DoctorIds, request.Level3DoctorIds, request.Level4DoctorIds
                };
                for (int i = 0; i < levelDoctors.Length; i++)
                {
                    if (i >= level) levelDoctors[i] = null;
                    else if (string.IsNullOrWhiteSpace(levelDoctors[i]))
                        return LabApprovalRejected($"Select at least one level {i + 1} doctor", 400);
                }

                var result = _sqlHelper.DML("IU_LabApprovalAuthorityMaster", CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        labApprovalAuthorityId = request.LabApprovalAuthorityId,
                        branchId = request.BranchId,
                        labTypeId = request.LabTypeId,
                        subSubCategoryIds = request.SubSubCategoryIds,
                        approvalFlowId = request.ApprovalFlowId,
                        approvalFlow = request.ApprovalFlow,
                        // a sequential flow always needs every level
                        isAllApprovalRequired = request.ApprovalFlowId == 1 ? 1 : request.IsAllApprovalRequired,
                        approvalLevelId = level,
                        approvalLevel = request.ApprovalLevel,
                        level1DoctorIds = levelDoctors[0],
                        level2DoctorIds = levelDoctors[1],
                        level3DoctorIds = levelDoctors[2],
                        level4DoctorIds = levelDoctors[3],
                        provisionalUserIds = string.IsNullOrWhiteSpace(request.ProvisionalUserIds) ? null : request.ProvisionalUserIds,
                        isActive = request.IsActive,
                        userId = globalValues.userId,
                        sessionBranchId = request.SessionBranchId,
                        roleId = request.RoleId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { Result = 0 });

                return LabApprovalSaved((int)Convert.ToInt32(result), request.LabApprovalAuthorityId == 0);
            }
            catch (Exception ex)
            {
                return LabApprovalError<LabApprovalAuthorityResponse>(ex, nameof(CreateUpdateLabApprovalAuthority));
            }
        }

        public ServiceResult<LabApprovalAuthorityResponse> UpdateLabApprovalAuthorityStatus(
            UpdateLabApprovalAuthorityStatusRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateLabApprovalAuthorityStatus", CommandType.StoredProcedure,
                    new
                    {
                        labApprovalAuthorityId = request.LabApprovalAuthorityId,
                        isActive = request.IsActive,
                        userId = globalValues.userId,
                        sessionBranchId = request.SessionBranchId,
                        roleId = request.RoleId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { Result = 0 });

                return LabApprovalSaved((int)Convert.ToInt32(result), false);
            }
            catch (Exception ex)
            {
                return LabApprovalError<LabApprovalAuthorityResponse>(ex, nameof(UpdateLabApprovalAuthorityStatus));
            }
        }

        private ServiceResult<LabApprovalAuthorityResponse> LabApprovalSaved(int result, bool isNew)
        {
            if (result > 0)
            {
                var ok = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<LabApprovalAuthorityResponse>.Success(
                    new LabApprovalAuthorityResponse { LabApprovalAuthorityId = result }, ok.Type, ok.Message, isNew ? 201 : 200);
            }

            return result switch
            {
                -1 => LabApprovalRejected("A selected sub department already has a rule for this branch and category", 409),
                -2 => LabApprovalRejected("A selected sub department, doctor or user is no longer valid", 400),
                -3 => LabApprovalRejected("You do not have the right to set up this category", 403),
                0 => LabApprovalRejected("Lab approval authority not found", 404),
                _ => LabApprovalRejected("Operation failed", 500)
            };
        }

        private ServiceResult<LabApprovalAuthorityResponse> LabApprovalRejected(string message, int status)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode(status == 409 ? "RECORD_ALREADY_EXISTS" : "INVALID_PARAMETER");
            return ServiceResult<LabApprovalAuthorityResponse>.Failure(alert.Type, message, status);
        }

        private ServiceResult<T> LabApprovalError<T>(Exception ex, string method)
        {
            LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{method}");
            var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
            return ServiceResult<T>.Failure(alert.Type, alert.Message, 500);
        }
    }
}
