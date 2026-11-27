/* =====================================================================
   Doctor Master and Corporate Master - Excel upload + contact / email verification  -  2026-11-27
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (CREATE OR ALTER)
   Run AFTER 2026-11-13_VendorMaster_Verification.sql (uses MobileVerificationOTP.IsVerified,
   EmailVerificationOTP.IsVerified).

   Objects
     1. PROC S_getDoctorMaster      unchanged except: + IsContactVerified, IsEmailVerified
     2. PROC S_GetCoporateDetails   unchanged except: + IsContactVerified (Contact 1), IsEmailVerified
     3. PROC IU_CorporateMaster     unchanged except: ends with SELECT @Result (needed to save inside the
                                    upload transaction; the single-record form uses the output parameter
                                    and is not affected)
     4. PROC S_ValidateDoctorUpload     new  (POST api/Doctor/doctorExcelUpload)
     5. PROC S_ValidateCorporateUpload  new  (POST api/Admin/corporateExcelUpload)

   After running: delete Redis keys _DoctorMaster_All and _CorporateMaster_All.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Doctor list with verification status ---------- */
CREATE OR ALTER PROCEDURE [dbo].[S_getDoctorMaster]
AS
BEGIN
	SELECT Dm.DoctorId
		,Dm.Title
		,Dm.Name
		,convert(VARCHAR, Dm.DOB, 105) AS Dob
		,Dm.Gender
		,Dm.CompleteName
		,Dm.ContactNo
		,Dm.EmailId
		,Dm.Address
		,Dm.SpecializationId
		,Dm.Specialization
		,Um.UserName
		,UM.Password
		,Dm.DepartmentId
		,Dm.Department
		,Dm.ProfileSummery
		,Dm.RegistrationNo
		,Dm.IsActive
		,Dm.UserId
		,Dm.HospId
		,Dm.IpAddress
		,dm.BranchId
		,isnull(dm.DoctorRoleId,0) as DoctorRoleId
		,dm.DoctorRole
		,ISNULL(Dm.CanApproveLabReport, 0) CanApproveLabReport
		,ISNULL(Dm.CanApproveDischargeSummary, 0) CanApproveDischargeSummary
		,ISNULL(Dm.DoctorPhotoFilePath, '') DoctorPhotoFilePath
		,Dm.IsDoctorUnit
		,Dm.RoomNo
		,(SELECT TOP 1 name FROM UserMaster umm WHERE umm.id = dm.CreatedBy) AS CreatedBy
        ,FORMAT(dm.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
        ,isnull((SELECT TOP 1 name FROM UserMaster umm WHERE umm.id = dm.LastModifiedBy),'') AS LastModifiedBy
        ,isnull(FORMAT(dm.LastModifiedOn, 'dd-MM-yyyy'),'') AS LastModifiedOn
		,CASE WHEN ISNULL(LTRIM(RTRIM(Dm.ContactNo)), '') <> ''
		       AND EXISTS (SELECT 1 FROM MobileVerificationOTP mv
		                   WHERE mv.MobileNumber = LTRIM(RTRIM(Dm.ContactNo)) AND mv.IsVerified = 1)
		      THEN 1 ELSE 0 END AS IsContactVerified
		,CASE WHEN ISNULL(LTRIM(RTRIM(Dm.EmailId)), '') <> ''
		       AND EXISTS (SELECT 1 FROM EmailVerificationOTP ev
		                   WHERE ev.Email = LTRIM(RTRIM(Dm.EmailId)) AND ev.IsVerified = 1)
		      THEN 1 ELSE 0 END AS IsEmailVerified
	FROM DoctorMaster Dm
	LEFT JOIN UserMaster Um ON Dm.UserId = Um.Id
END
GO

/* ---------- 2. Corporate list with verification status ---------- */
CREATE OR ALTER PROCEDURE [dbo].[S_GetCoporateDetails]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  CorporateId
           ,CorporateName
           ,InsuranceCompanyName
           ,InsuranceCompanyId
           ,CorporateTypeId
           ,PaymentTypeId
           ,CorporateCode
           ,CorporateContact1
           ,CorporateContact2
           ,CorporateEmail
           ,CorporateAddress1
           ,CorporateAddress2
           ,IsActive
           ,format(ContractStartFrom,'dd-MM-yyyy') AS ContractStartFrom
           ,format(ContractExpiresOn,'dd-MM-yyyy') AS ContractExpiresOn
           ,CopaymentPer
           ,DiscountPerOut
           ,DiscountPerIn
           ,HikePerOut
           ,HikePerIn
           ,ActivePaymentModes
           ,IsRegistrationChargeApplicable
           ,IsCaseBillingApplicable
           ,CASE WHEN ISNULL(LTRIM(RTRIM(cm.CorporateContact1)), '') <> ''
                  AND EXISTS (SELECT 1 FROM MobileVerificationOTP mv
                              WHERE mv.MobileNumber = LTRIM(RTRIM(cm.CorporateContact1)) AND mv.IsVerified = 1)
                 THEN 1 ELSE 0 END AS IsContactVerified
           ,CASE WHEN ISNULL(LTRIM(RTRIM(cm.CorporateEmail)), '') <> ''
                  AND EXISTS (SELECT 1 FROM EmailVerificationOTP ev
                              WHERE ev.Email = LTRIM(RTRIM(cm.CorporateEmail)) AND ev.IsVerified = 1)
                 THEN 1 ELSE 0 END AS IsEmailVerified
    FROM CorporateMaster cm
END
GO

/* ---------- 3. IU_CorporateMaster: also return @Result as a row ---------- */
CREATE OR ALTER PROCEDURE [dbo].[IU_CorporateMaster]
(
    @hospId int,
    @corporateId int,
    @corporateName nvarchar(256),
    @insuranceCompanyName nvarchar(256),
    @insuranceCompanyId int,
    @corporateTypeName nvarchar(256),
    @corporateTypeId int,
    @paymentTypeId int,
    @corporateCode nvarchar(50),
    @corporateContact1 nvarchar(20),
    @corporateContact2 nvarchar(20),
    @corporateEmail nvarchar(100),
    @corporateAddress1 nvarchar(500),
    @corporateAddress2 nvarchar(500),
    @isActive int,
    @contractStartFrom datetime,
    @contractExpiresOn datetime,
    @copaymentPer decimal(16,6),
    @discountPerOut decimal(16,6),
    @discountPerIn decimal(16,6),
    @hikePerOut decimal(16,6),
    @hikePerIn decimal(16,6),
    @activePaymentModes nvarchar(100),
    @isRegistrationChargeApplicable int = 0,
    @isCaseBillingApplicable int = 0,
    @userId int,
    @IpAddress nvarchar(20),
    @Result int = null output
)
AS
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[CorporateMaster] WHERE CorporateName = @corporateName
                    AND InsuranceCompanyId = @insuranceCompanyId AND CorporateId <> @corporateId)
    BEGIN
        SET @Result = -1;
    END
    ELSE IF (@corporateId = 0)
    BEGIN
        INSERT INTO CorporateMaster
            (HospId, CorporateName, InsuranceCompanyName, InsuranceCompanyId, corporateTypeName, corporateTypeId,
             paymentTypeId, CorporateCode, CorporateContact1, CorporateContact2, CorporateEmail, CorporateAddress1,
             CorporateAddress2, IsActive, ContractStartFrom, ContractExpiresOn, CopaymentPer, DiscountPerOut,
             DiscountPerIn, HikePerOut, HikePerIn, ActivePaymentModes, IsRegistrationChargeApplicable,
             IsCaseBillingApplicable, CreatedBy, IpAddress)
        VALUES
            (@hospId, @corporateName, @insuranceCompanyName, @insuranceCompanyId, @corporateTypeName, @corporateTypeId,
             @paymentTypeId, @corporateCode, @corporateContact1, @corporateContact2, @corporateEmail, @corporateAddress1,
             @corporateAddress2, @isActive, @contractStartFrom, @contractExpiresOn, @copaymentPer, @discountPerOut,
             @discountPerIn, @hikePerOut, @hikePerIn, @activePaymentModes, @isRegistrationChargeApplicable,
             @isCaseBillingApplicable, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE CorporateMaster
        SET HospId = @hospId, CorporateName = @corporateName, InsuranceCompanyName = @insuranceCompanyName,
            InsuranceCompanyId = @insuranceCompanyId, corporateTypeName = @corporateTypeName,
            corporateTypeId = @corporateTypeId, paymentTypeId = @paymentTypeId, CorporateCode = @corporateCode,
            CorporateContact1 = @corporateContact1, CorporateContact2 = @corporateContact2,
            CorporateEmail = @corporateEmail, CorporateAddress1 = @corporateAddress1,
            CorporateAddress2 = @corporateAddress2, IsActive = @isActive, ContractStartFrom = @contractStartFrom,
            ContractExpiresOn = @contractExpiresOn, CopaymentPer = @copaymentPer, DiscountPerOut = @discountPerOut,
            DiscountPerIn = @discountPerIn, HikePerOut = @hikePerOut, HikePerIn = @hikePerIn,
            ActivePaymentModes = @activePaymentModes,
            IsRegistrationChargeApplicable = @isRegistrationChargeApplicable,
            IsCaseBillingApplicable = @isCaseBillingApplicable,
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE CorporateId = @corporateId;

        SET @Result = @corporateId;
    END

    SELECT @Result;
END
GO

/* ---------- 4. Doctor upload lookups ----------
   @rowsJson: [{ "RowNo":1, "Name":"", "ContactNo":"" }, ...]
   Result sets: 1 specializations, 2 departments, 3 pick lists (title / gender / doctor role),
                4 rows that already exist (same name + contact, the IU_DoctorMaster rule), 5 active branches */
CREATE OR ALTER PROCEDURE dbo.S_ValidateDoctorUpload
(
    @hospId   int,
    @rowsJson nvarchar(max)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT SpecializationId, Specialization FROM dbo.DoctorSpecializationMaster WHERE IsActive = 1;

    SELECT DepartmentId, Department FROM dbo.DoctorDepartmentMaster WHERE IsActive = 1;

    SELECT FieldName, [Key], Value
    FROM   dbo.PickListMaster
    WHERE  IsActive = 1 AND FieldName IN ('title', 'gender', 'DoctorRoleForDoctorMaster')
    ORDER BY FieldName, OrderId;

    SELECT j.RowNo, d.DoctorId
    FROM OPENJSON(@rowsJson) WITH ( RowNo int, Name nvarchar(256), ContactNo nvarchar(30) ) j
    CROSS APPLY (SELECT TOP 1 dm.DoctorId FROM dbo.DoctorMaster dm
                 WHERE dm.Name = j.Name AND dm.ContactNo = j.ContactNo
                 ORDER BY dm.DoctorId) d;

    SELECT BranchId FROM dbo.BranchMaster WHERE HospId = @hospId AND IsActive = 1;
END
GO

/* ---------- 5. Corporate upload lookups ----------
   @rowsJson: [{ "RowNo":1, "CorporateName":"" }, ...]
   Result sets: 1 corporate types, 2 insurance companies, 3 active payment modes, 4 payment types,
                5 existing corporates with one of the uploaded names (name + insurance company must be unique) */
CREATE OR ALTER PROCEDURE dbo.S_ValidateCorporateUpload
(
    @rowsJson nvarchar(max)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CorporateTypeId, CorporateTypeName FROM dbo.CorporateTypeMaster;

    SELECT InsuranceCompanyId, InsuranceCompanyName FROM dbo.InsuranceCompanyMaster;

    SELECT PaymentModeId FROM dbo.PaymentModeMaster WHERE IsActive = 1;

    SELECT [Key], Value FROM dbo.PickListMaster
    WHERE  IsActive = 1 AND FieldName = 'CorporatePaymentType'
    ORDER BY OrderId;

    SELECT cm.CorporateName, ISNULL(cm.InsuranceCompanyId, 0) AS InsuranceCompanyId
    FROM   dbo.CorporateMaster cm
    WHERE  EXISTS (SELECT 1 FROM OPENJSON(@rowsJson) WITH ( CorporateName nvarchar(256) ) j
                   WHERE j.CorporateName = cm.CorporateName);
END
GO

PRINT 'Doctor / Corporate upload and verification objects created/updated.';
GO
