/* =====================================================================
   User Master: Provisional Approval with signature and consent upload  -  2026-11-27
   Target DB : GWT_HIS_API (NEW HIS)      Re-runnable: yes

     1. dbo.UserMaster  + CanProvisionalApproval (default 0), ProvisionalSignPath, ConsentFilePath
     2. IU_UserMaster        3 optional parameters; @Result -2 = Yes without a signature
     3. S_GetUserMasterList  returns the 3 columns

   Run BEFORE deploying the new API (it sends the new parameters). Then delete Redis key _UserMaster_All.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.UserMaster', 'CanProvisionalApproval') IS NULL
    ALTER TABLE dbo.UserMaster ADD CanProvisionalApproval BIT NOT NULL CONSTRAINT DF_UserMaster_CanProvisionalApproval DEFAULT (0);
GO

IF COL_LENGTH('dbo.UserMaster', 'ProvisionalSignPath') IS NULL
    ALTER TABLE dbo.UserMaster ADD ProvisionalSignPath NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.UserMaster', 'ConsentFilePath') IS NULL
    ALTER TABLE dbo.UserMaster ADD ConsentFilePath NVARCHAR(500) NULL;
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
    @CanProvisionalApproval INT = 0,            -- 1 = Yes, 0 = No (default)
    @ProvisionalSignPath NVARCHAR(500) = NULL,  -- NULL = keep the saved file
    @ConsentFilePath NVARCHAR(500) = NULL,      -- NULL = keep the saved file

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

    -- Provisional approval needs a signature: a new file or the one already saved
    IF (ISNULL(@CanProvisionalApproval, 0) = 1
        AND NULLIF(@ProvisionalSignPath, '') IS NULL
        AND NOT EXISTS (SELECT 1 FROM UserMaster WHERE Id = @UserId AND NULLIF(ProvisionalSignPath, '') IS NOT NULL))
    BEGIN
        SET @Result = -2;
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
            IsContactVerificationRequired, CanProvisionalApproval, ProvisionalSignPath, ConsentFilePath
        )
        VALUES (
            @HospId, @Address, @Contact, @DOB, @Email, @FirstName,
            @MidelName, @LastName, GETDATE(), @Password, @UserName,
            @Gender, CONCAT(@FirstName, ' ', @MidelName, ' ', @LastName, ' (', @UserName, ')'),
            @IsActive, GETDATE(), 1, @EmployeeID, @UserDepartmentId, @ReportToUserId, 0, 0, @UserRoleId, @UserRole,  -- New users have unverified contact and email
            @VerifyRequired,
            CASE WHEN @CanProvisionalApproval = 1 THEN 1 ELSE 0 END, @ProvisionalSignPath, @ConsentFilePath
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
            IsContactVerificationRequired = @VerifyRequired,
            CanProvisionalApproval = CASE WHEN @CanProvisionalApproval = 1 THEN 1 ELSE 0 END,
            ProvisionalSignPath = ISNULL(@ProvisionalSignPath, ProvisionalSignPath),
            ConsentFilePath = ISNULL(@ConsentFilePath, ConsentFilePath)
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
          ,CAST(CanProvisionalApproval AS INT) AS CanProvisionalApproval
          ,ProvisionalSignPath
          ,ConsentFilePath
    FROM [dbo].[UserMaster] um
    ORDER BY [Id]
END
GO
