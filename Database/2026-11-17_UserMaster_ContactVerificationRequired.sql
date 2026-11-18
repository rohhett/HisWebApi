/* =====================================================================
   2026-11-17  User Master: "Contact verification required on login" (Yes / No)
   ---------------------------------------------------------------------
   * UserMaster.IsContactVerificationRequired BIT NOT NULL DEFAULT 1
       1 = Yes: at login, if mobile/email is not verified, the OTP verification popup opens (current behaviour)
       0 = No : user goes straight in, no verification popup
     Existing users get 1, so nothing changes until an admin sets No.
   * IU_UserMaster        - new optional @IsContactVerificationRequired INT = 1 (saved on insert and update)
   * S_GetUserMasterList  - returns IsContactVerificationRequired
   * S_UserLogin          - returns IsContactVerificationRequired
   Callers (checked): IU_UserMaster, S_GetUserMasterList -> AdminRepository;
                      S_UserLogin -> UserRepository.UserLogin. No other procedure calls them.
   Re-runnable. The API caches the user list in Redis key _UserMaster_All - it is cleared on the
   next user save; to see the new field at once, remove that key (or restart Redis).
   ===================================================================== */

IF COL_LENGTH('dbo.UserMaster', 'IsContactVerificationRequired') IS NULL
    ALTER TABLE dbo.UserMaster
        ADD IsContactVerificationRequired BIT NOT NULL
            CONSTRAINT DF_UserMaster_IsContactVerificationRequired DEFAULT (1);
GO

CREATE OR ALTER PROCEDURE [dbo].[IU_UserMaster]
(
    @HospId INT,
    @Address NVARCHAR(500) = NULL,
    @Contact NVARCHAR(15) = NULL,
    @DOB DATE = NULL,
    @Email NVARCHAR(100) = NULL,
    @FirstName VARCHAR(100),
    @MidelName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100) = NULL,
    @Password NVARCHAR(100),
    @UserName NVARCHAR(100),
    @Gender VARCHAR(20) = NULL,
    @UserId INT = NULL,
    @IsActive INT,
    @EmployeeID NVARCHAR(100) = NULL,
    @ReportToUserId INT,
    @UserDepartmentId INT,
    @CanChangePassword INT = 0,
    @UserRoleId INT = 0,
    @UserRole NVARCHAR(100) = NULL,
    @IsContactVerificationRequired INT = 1,     -- 1 = Yes (default), 0 = No

    @Result INT = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VerifyRequired BIT = CASE WHEN ISNULL(@IsContactVerificationRequired, 1) = 0 THEN 0 ELSE 1 END;

    -- Check for duplicate username
    IF EXISTS (SELECT 1 FROM UserMaster WHERE UserName = @UserName AND (@UserId IS NULL OR Id != @UserId))
    BEGIN
        SET @Result = -1;
        SELECT @Result;
        RETURN;
    END

    -- Insert new user
    IF (@UserId IS NULL OR @UserId = 0)
    BEGIN
        INSERT INTO UserMaster (
            [HospId], [Address], [Contact], [DOB], [Email], [FirstName],
            [MidelName], [LastName], [LoginOn], [Password], [UserName],
            [Gender], [Name], IsActive, CreatedOn, CreatedBy, EmployeeID,
            UserDepartmentId, ReportToUserId, IsContactVerified, IsEmailVerified, UserRoleId, UserRole,
            IsContactVerificationRequired
        )
        VALUES (
            @HospId, @Address, @Contact, @DOB, @Email, @FirstName,
            @MidelName, @LastName, GETDATE(), @Password, @UserName,
            @Gender, CONCAT(@FirstName, ' ', @MidelName, ' ', @LastName, ' (', @UserName, ')'),
            @IsActive, GETDATE(), 1, @EmployeeID, @UserDepartmentId, @ReportToUserId, 0, 0, @UserRoleId, @UserRole,  -- New users have unverified contact and email
            @VerifyRequired
        );

        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        -- Declare variables to check if contact or email changed
        DECLARE @OldContact NVARCHAR(15);
        DECLARE @OldEmail NVARCHAR(100);
        DECLARE @NewIsContactVerified INT;
        DECLARE @NewIsEmailVerified INT;

        -- Get current contact and email
        SELECT
            @OldContact = Contact,
            @OldEmail = Email,
            @NewIsContactVerified = IsContactVerified,
            @NewIsEmailVerified = IsEmailVerified
        FROM UserMaster
        WHERE Id = @UserId;

        -- Set IsContactVerified to 0 if contact number changed
        IF (@Contact IS NOT NULL AND @Contact != @OldContact)
        BEGIN
            SET @NewIsContactVerified = 0;
        END

        -- Set IsEmailVerified to 0 if email changed
        IF (@Email IS NOT NULL AND @Email != @OldEmail)
        BEGIN
            SET @NewIsEmailVerified = 0;
        END

        -- Update existing user
        UPDATE UserMaster
        SET
            [HospId] = @HospId,
            [Address] = @Address,
            [Contact] = @Contact,
            [DOB] = @DOB,
            [Email] = @Email,
            [FirstName] = @FirstName,
            [MidelName] = @MidelName,
            [LastName] = @LastName,
            [UserName] = @UserName,
            [Gender] = @Gender,
            [Name] = CONCAT(@FirstName, ' ', @MidelName, ' ', @LastName, ' (', @UserName, ')'),
            [LastModifiedBy] = 1,
            [LastModifiedOn] = GETDATE(),
            IsActive = @IsActive,
            EmployeeID = @EmployeeID,
            ReportToUserId = @ReportToUserId,
            UserDepartmentId = @UserDepartmentId,
            IsContactVerified = @NewIsContactVerified,
            IsEmailVerified = @NewIsEmailVerified,
            UserRoleId = @UserRoleId,
            UserRole = @UserRole,
            IsContactVerificationRequired = @VerifyRequired
        WHERE Id = @UserId;

        IF (@CanChangePassword = 1)
        BEGIN
            UPDATE UserMaster SET Password = @Password WHERE Id = @UserId;
        END

        SET @Result = @UserId;
    END

    SELECT @Result;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetUserMasterList]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id]
          ,[HospId]
          ,[FirstName]
          ,[MidelName]
          ,[LastName]
          ,[Name]
          ,format(um.[DOB], 'dd-MM-yyyy') as DOB
          ,[Gender]
          ,[UserName]
          ,'PE5wEZ#5vdNt@hwgT' as Password
          ,[Address]
          ,[Contact]
          ,[Email]
          ,[IsActive]
          ,[EmployeeID]
          ,(select top 1 name from UserMaster um where um.id=um.CreatedBy) as CreatedBy
          ,format(um.CreatedOn, 'dd-MM-yyyy hh:mm tt') as CreatedOn
          ,(select top 1 name from UserMaster um where um.id=um.LastModifiedBy) as LastModifiedBy
          ,format(um.LastModifiedOn, 'dd-MM-yyyy hh:mm tt') as LastModifiedOn
          ,[IPAddress]
          ,[ReportToUserId]
          ,[UserDepartmentId]
          ,UserRoleId
          ,UserRole
          ,CAST(IsContactVerificationRequired AS INT) AS IsContactVerificationRequired
    FROM [dbo].[UserMaster] um
    ORDER BY [Id]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_UserLogin]
(
    @UserName NVARCHAR(100),
    @BranchId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        UserMaster.Id,
        UserMaster.HospId,
        FirstName,
        MidelName,
        LastName,
        Gender,
        Email,
        Contact,
        IsContactVerified,
        IsEmailVerified,
        IsContactVerificationRequired,
        Address,
        DOB,
        UserName,
        isSuperAdmin,
        Password,
        UserMaster.IsActive,
        LoginOn,
        Name,
        @BranchId as BranchId
    FROM UserMaster
    WHERE UserName = @UserName
      and IsActive = 1
END
GO
