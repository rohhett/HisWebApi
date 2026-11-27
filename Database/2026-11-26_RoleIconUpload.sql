/* =====================================================================
   Role icon add / update from Role Master (+ button)  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF COL_LENGTH / CREATE OR ALTER)
   Needs     : 2026-11-26_RoleIconBase64.sql (table dbo.RoleIconMaster) run first

   Objects
     1. COLUMNS    dbo.RoleIconMaster.CreatedBy, IpAddress, LastModifiedBy, LastModifiedOn (audit)
     2. PROCEDURE  dbo.IU_RoleIconMaster
                   @roleIconId = 0 -> insert; > 0 -> update name (and image when @iconBase64 is given)
                   @Result: > 0 RoleIconId, -1 icon name already exists, 0 unknown @roleIconId
     3. PROCEDURE  dbo.I_RoleIconMaster dropped (insert-only first version, replaced by 2.)

   The API checks the file (PNG signature, at most 5 KB) before calling the procedure and clears
   the Redis key _RoleIconMaster_All after every call. Roles show the updated icon at once
   (they hold only RoleIconId).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.RoleIconMaster', 'CreatedBy') IS NULL
    ALTER TABLE dbo.RoleIconMaster ADD CreatedBy int NULL;
GO
IF COL_LENGTH('dbo.RoleIconMaster', 'IpAddress') IS NULL
    ALTER TABLE dbo.RoleIconMaster ADD IpAddress nvarchar(20) NULL;
GO
IF COL_LENGTH('dbo.RoleIconMaster', 'LastModifiedBy') IS NULL
    ALTER TABLE dbo.RoleIconMaster ADD LastModifiedBy int NULL;
GO
IF COL_LENGTH('dbo.RoleIconMaster', 'LastModifiedOn') IS NULL
    ALTER TABLE dbo.RoleIconMaster ADD LastModifiedOn datetime NULL;
GO

CREATE OR ALTER PROCEDURE dbo.IU_RoleIconMaster
(
    @roleIconId int = 0,
    @iconName   nvarchar(100),
    @mimeType   nvarchar(50) = NULL,
    @iconBase64 nvarchar(max) = NULL,   -- NULL on update = keep the current image
    @userId     int,
    @IpAddress  nvarchar(20),
    @Result     int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @iconName = LTRIM(RTRIM(@iconName));
    SET @iconBase64 = NULLIF(@iconBase64, N'');

    IF EXISTS (SELECT 1 FROM dbo.RoleIconMaster WHERE IconName = @iconName AND RoleIconId <> @roleIconId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@roleIconId = 0)
    BEGIN
        IF (@iconBase64 IS NULL)
        BEGIN
            SET @Result = -2;   -- a new icon needs an image (the API checks this first)
            RETURN;
        END

        INSERT INTO dbo.RoleIconMaster (IconName, MimeType, IconBase64, IsActive, CreatedBy, IpAddress)
        VALUES (@iconName, ISNULL(@mimeType, N'image/png'), @iconBase64, 1, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.RoleIconMaster WHERE RoleIconId = @roleIconId)
    BEGIN
        SET @Result = 0;
        RETURN;
    END

    UPDATE dbo.RoleIconMaster
    SET IconName = @iconName,
        MimeType = CASE WHEN @iconBase64 IS NULL THEN MimeType ELSE ISNULL(@mimeType, N'image/png') END,
        IconBase64 = ISNULL(@iconBase64, IconBase64),
        LastModifiedBy = @userId,
        LastModifiedOn = GETDATE(),
        IpAddress = @IpAddress
    WHERE RoleIconId = @roleIconId;

    SET @Result = @roleIconId;
END
GO

IF OBJECT_ID('dbo.I_RoleIconMaster', 'P') IS NOT NULL
    DROP PROCEDURE dbo.I_RoleIconMaster;
GO
