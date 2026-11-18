/*
    2026-11-17 — Login history: write a row to dbo.UserLoginSessions on every successful login
    Database : GWT_HIS_API
    Re-runnable: yes (CREATE OR ALTER / IF NOT EXISTS)

    Objects
      1. PROCEDURE dbo.I_UserLoginSession              (new)  inserts one session row, returns SessionId
      2. PROCEDURE dbo.S_GetUserLoginSessionStatus     (new)  status of one session (logout token check
                                                              when Redis is not reachable)
      3. INDEX     IX_UserLoginSessions_UserId_LoginTime (new) for "login history of a user, latest first"

    Called from:
      - UserRepository.UserLogin (POST api/User/userLogin) after the password is verified -> I_UserLoginSession
      - TokenRevocationService.IsRevoked (every authorized request, only when Redis is down) -> S_GetUserLoginSessionStatus
      - Logout (POST api/User/logout) keeps using the existing U_UserLoginSession
    Table dbo.UserLoginSessions already exists (no column changes). Existing U_UserLoginSession
    and S_ValidateRefreshToken are unchanged.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.I_UserLoginSession
    @UserId          INT,
    @BranchId        INT,
    @IpAddress       NVARCHAR(50)  = NULL,
    @UserAgent       NVARCHAR(500) = NULL,
    @Browser         NVARCHAR(100) = NULL,
    @BrowserVersion  NVARCHAR(50)  = NULL,
    @OperatingSystem NVARCHAR(100) = NULL,
    @Device          NVARCHAR(200) = NULL,
    @DeviceType      NVARCHAR(50)  = NULL,
    @Location        NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.UserLoginSessions
        (UserId, BranchId, LoginTime, IpAddress, UserAgent, Browser, BrowserVersion,
         OperatingSystem, Device, DeviceType, Location, Status, LastActivityTime)
    VALUES
        (@UserId, @BranchId, GETDATE(), @IpAddress, @UserAgent, @Browser, @BrowserVersion,
         @OperatingSystem, @Device, @DeviceType, @Location, 'Active', GETDATE());

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS SessionId;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetUserLoginSessionStatus
    @SessionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT SessionId, UserId, Status, LogoutTime
    FROM dbo.UserLoginSessions
    WHERE SessionId = @SessionId;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.UserLoginSessions')
                 AND name = 'IX_UserLoginSessions_UserId_LoginTime')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UserLoginSessions_UserId_LoginTime
        ON dbo.UserLoginSessions (UserId, LoginTime DESC)
        INCLUDE (BranchId, Status, LogoutTime);
END
GO
