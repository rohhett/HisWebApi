/* =====================================================================
   Medical Vendor Master - contact / email verification status  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)
   Run AFTER 2026-11-13_VendorMaster_CreditDays.sql (the list below includes CreditDays)

   Verification is optional and uses the existing endpoints
     Home/sendMobileVerificationOtp, Home/verifyMobileVerificationOtp,
     Home/sendEmailVerificationOtp,  Home/verifyEmailVerificationOtp.
   A number / address counts as verified when an OTP for it was verified successfully.

   Why a new marker: the verify procedures set IsUsed = 1 both on success AND when an OTP expires,
   so IsUsed alone cannot prove a successful verification.

   Objects
     1. TABLE MobileVerificationOTP, EmailVerificationOTP   ADD IsVerified bit NOT NULL DEFAULT 0
     2. PROC  U_VerifyMobileVerificationOtp, U_VerifyEmailVerificationOtp
                 unchanged except: the success path also sets IsVerified = 1
                 (callers: HomeRepository only - the four endpoints above)
     3. PROC  S_getdllVendorMasterList   + IsContactVerified, IsEmailVerified (computed live from the OTP logs,
                 so a later change of contact / email shows "not verified" until the new one is verified)
   OTPs verified before this script ran have IsVerified = 0 (they cannot be told apart from expired ones).
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Marker ---------- */
IF COL_LENGTH('dbo.MobileVerificationOTP', 'IsVerified') IS NULL
    ALTER TABLE dbo.MobileVerificationOTP ADD IsVerified bit NOT NULL CONSTRAINT DF_MobileVerificationOTP_IsVerified DEFAULT (0);
GO
IF COL_LENGTH('dbo.EmailVerificationOTP', 'IsVerified') IS NULL
    ALTER TABLE dbo.EmailVerificationOTP ADD IsVerified bit NOT NULL CONSTRAINT DF_EmailVerificationOTP_IsVerified DEFAULT (0);
GO

/* ---------- 2. Verify procedures (success path also sets IsVerified = 1) ---------- */
CREATE OR ALTER PROCEDURE dbo.U_VerifyMobileVerificationOtp
    @MobileNumber  VARCHAR(15),
    @Otp           VARCHAR(6),
    @ExpiryMinutes INT = 5,
    @Result        INT OUTPUT,
    @Message       NVARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @Id INT, @StoredOtp VARCHAR(6), @CreatedOn DATETIME;

        SELECT TOP 1 @Id = Id, @StoredOtp = Otp, @CreatedOn = CreatedOn
        FROM MobileVerificationOTP
        WHERE MobileNumber = @MobileNumber AND IsUsed = 0
        ORDER BY CreatedOn DESC;

        IF @Id IS NULL
        BEGIN
            SET @Result = -1;
            SET @Message = 'No active OTP found for this mobile number';
            RETURN;
        END

        IF DATEDIFF(MINUTE, @CreatedOn, GETDATE()) > @ExpiryMinutes
        BEGIN
            UPDATE MobileVerificationOTP SET IsUsed = 1, UsedOn = GETDATE() WHERE Id = @Id;
            SET @Result = -3;
            SET @Message = 'OTP has expired';
            RETURN;
        END

        IF @StoredOtp <> @Otp
        BEGIN
            SET @Result = -6;
            SET @Message = 'Invalid OTP';
            RETURN;
        END

        -- OTP valid - consume it, record the successful verification, mark contact verified
        UPDATE MobileVerificationOTP SET IsUsed = 1, UsedOn = GETDATE(), IsVerified = 1 WHERE Id = @Id;
        UPDATE UserMaster SET IsContactVerified = 1 WHERE Contact = @MobileNumber;

        SET @Result = 1;
        SET @Message = 'Mobile number verified successfully';
    END TRY
    BEGIN CATCH
        SET @Result = 0;
        SET @Message = ERROR_MESSAGE();
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.U_VerifyEmailVerificationOtp
    @Email         VARCHAR(100),
    @Otp           VARCHAR(6),
    @ExpiryMinutes INT = 5,
    @Result        INT OUTPUT,
    @Message       NVARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @Id INT, @StoredOtp VARCHAR(6), @CreatedOn DATETIME;

        SELECT TOP 1 @Id = Id, @StoredOtp = Otp, @CreatedOn = CreatedOn
        FROM EmailVerificationOTP
        WHERE Email = @Email AND IsUsed = 0
        ORDER BY CreatedOn DESC;

        IF @Id IS NULL
        BEGIN
            SET @Result = -1;
            SET @Message = 'No active OTP found for this email';
            RETURN;
        END

        IF DATEDIFF(MINUTE, @CreatedOn, GETDATE()) > @ExpiryMinutes
        BEGIN
            UPDATE EmailVerificationOTP SET IsUsed = 1, UsedOn = GETDATE() WHERE Id = @Id;
            SET @Result = -3;
            SET @Message = 'OTP has expired';
            RETURN;
        END

        IF @StoredOtp <> @Otp
        BEGIN
            SET @Result = -6;
            SET @Message = 'Invalid OTP';
            RETURN;
        END

        UPDATE EmailVerificationOTP SET IsUsed = 1, UsedOn = GETDATE(), IsVerified = 1 WHERE Id = @Id;
        UPDATE UserMaster SET IsEmailVerified = 1 WHERE Email = @Email;

        SET @Result = 1;
        SET @Message = 'Email verified successfully';
    END TRY
    BEGIN CATCH
        SET @Result = 0;
        SET @Message = ERROR_MESSAGE();
    END CATCH
END
GO

/* ---------- 3. Vendor list with verification status ---------- */
CREATE OR ALTER PROCEDURE [dbo].[S_getdllVendorMasterList]
(
  @activeStatus nvarchar(max)
)
 as
 begin
 Select
 VendorId
 ,TypeId
 ,Type
 ,VendorName
 ,ContactNo
 ,Email
 ,DLNO
 ,GSTINNo
 ,PrintAddress as fullAddress
 ,Address
 ,CountryId
 ,StateId
 ,DistrictId
 ,CityId
 ,MappingBranch
 ,IsActive
 ,Pincode
 ,CreditDays
 ,CASE WHEN ISNULL(LTRIM(RTRIM(EVM.ContactNo)), '') <> ''
            AND EXISTS (SELECT 1 FROM MobileVerificationOTP mv
                        WHERE mv.MobileNumber = LTRIM(RTRIM(EVM.ContactNo)) AND mv.IsVerified = 1)
       THEN 1 ELSE 0 END AS IsContactVerified
 ,CASE WHEN ISNULL(LTRIM(RTRIM(EVM.Email)), '') <> ''
            AND EXISTS (SELECT 1 FROM EmailVerificationOTP ev
                        WHERE ev.Email = LTRIM(RTRIM(EVM.Email)) AND ev.IsVerified = 1)
       THEN 1 ELSE 0 END AS IsEmailVerified
   ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = EVM.CreatedBy) AS CreatedBy
        ,FORMAT(EVM.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
        ,isnull((SELECT TOP 1 name FROM UserMaster um WHERE um.id = EVM.LastModifiedBy),'') AS LastModifiedBy
        ,isnull(FORMAT(EVM.LastModifiedOn, 'dd-MM-yyyy'),'') AS LastModifiedOn
 from VendorMaster EVM
 end
GO
