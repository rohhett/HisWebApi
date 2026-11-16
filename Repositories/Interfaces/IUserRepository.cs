using System.Collections.Generic;
using HISWEBAPI.Models;
using HISWEBAPI.DTO;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IUserRepository
    {
        ServiceResult<UserLoginResponseData> UserLogin(UserLoginRequest request, string? ipAddress = null);
        ServiceResult<string> Logout(LogoutRequest request);
        ServiceResult<UserSignupResponseData> NewUserSignUp(UserSignupRequest request);
        ServiceResult<SmsOtpResponseData> SendSmsOtp(SendSmsOtpRequest request);
        ServiceResult<OtpVerificationResponseData> VerifySmsOtp(VerifySmsOtpRequest request);
        ServiceResult<EmailOtpResponseData> SendEmailOtp(SendEmailOtpRequest request);
        ServiceResult<string> VerifyEmailOtp(VerifyEmailOtpRequest request);
        ServiceResult<string> ResetPasswordByUserId(ResetPasswordRequest request);
        ServiceResult<string> UpdatePassword(UpdatePasswordRequest request);
        ServiceResult<IEnumerable<UserRoleModel>> GetUserRoles(UserRoleRequest request);
        ServiceResult<UserTabMenuMappingResponse> GetUserTabAndSubMenuMapping(int roleId, int branchId, int userId);
        ServiceResult<string> SaveUserFavoriteRoles(SaveUserFavoriteRolesRequest request, AllGlobalValues globalValues);
        ServiceResult<string> SaveRoleWiseUserFavoriteSubMenu(SaveRoleWiseUserFavoriteSubMenuRequest request, AllGlobalValues globalValues);
        ServiceResult<Dictionary<string, object>> GetUserAccessRights(int branchId, int roleId, AllGlobalValues globalValues);
        ServiceResult<Dictionary<string, object>> GetDashboardUserAccessRights(int branchId, int roleId, AllGlobalValues globalValues);

    }
}