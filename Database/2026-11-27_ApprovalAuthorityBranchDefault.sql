/* 2026-11-27  Approval Authority: branch-wise rules with a default (BranchId 0).
   Rule choice everywhere: if the request's branch has an active rule for the approval type (for its department
   or "All"), only that branch's rules are used; otherwise the default rules (BranchId 0).
   Re-runnable. After running, delete the Redis keys _ApprovalAuthorityMaster_TypeId*. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* One time only: types 6-10 ignored BranchId until now, so their saved rules applied to every branch.
   They become default rules to keep that behaviour. The backup table marks that this step has run. */
IF OBJECT_ID('dbo.ApprovalAuthorityMaster_Bak_20261127') IS NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    SELECT * INTO dbo.ApprovalAuthorityMaster_Bak_20261127 FROM dbo.ApprovalAuthorityMaster;
    UPDATE dbo.ApprovalAuthorityMaster SET BranchId = 0 WHERE ApprovalTypeId IN (6, 7, 8, 9, 10);
    COMMIT TRANSACTION;
END
GO

/* Which branch's rules apply: @branchId when it has a usable rule, else 0 (default). */
CREATE OR ALTER FUNCTION dbo.fn_ApprovalRuleBranch (@approvalTypeId int, @branchId int, @roleId int, @hospId int)
RETURNS int
AS
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.ApprovalAuthorityMaster
               WHERE ApprovalTypeId = @approvalTypeId AND BranchId = @branchId AND IsActive = 1
                 AND ISNULL(RoleId, 0) IN (@roleId, 0)
                 AND (@hospId IS NULL OR HospId = @hospId)
                 AND (@approvalTypeId = 6 OR ISNULL(AmountUpTo, 0) > 0))   -- type 6 has no amount
        RETURN @branchId;
    RETURN 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_OPDBookingDetailsForDiscountApproval]
( @fromDate DATE,
    @toDate   DATE,
	@branchId int,
	@corporateId int,
	@userId int
)
as
begin
SELECT 
	 OBD.BookingId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	,OBD.CorporateId
	,cm.CorporateName
	,OBD.InsuranceCompanyId
	,OBD.ReferDoctorId
	,OBD.TotalBillAmount
	,OBD.TotalDiscountPerOnBill
	,OBD.TotalDiscountAmountOnBill
	,OBD.RoundOff
	,OBD.TotalPatientPayableAmount
	,OBD.PolicyNo
	,OBD.PolicyCardNo
	,OBD.ExpiryDate
	,OBD.CardHolder
	,OBD.ReferalNo
	,OBD.ReferalDate
	,OBD.IsPaymentCollected
	,OBD.IsDiscountApprovalRequired
	,OBD.IsDiscountApproved
	,OBD.TotalApprovedDiscountPerOnBill
	,OBD.ApprovalRemarks
	,OBD.IsLevel1Approve
	,OBD.Level1ApproveId
	,OBD.Level1ApproveOn
	,OBD.IsLevel2Approve
	,OBD.Level2ApproveId
	,OBD.Level2ApproveOn
	,OBD.IsLevel3Approve
	,OBD.Level3ApproveId
	,OBD.Level3ApproveOn
	,OBD.IsLevel4Approve
	,OBD.Level4ApproveId
	,OBD.Level4ApproveOn
	,OBD.IsCancel
	,OBD.Status
	,OBD.StatusId
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CancelBy) AS CancelBy
    ,FORMAT(OBD.CancelOn, 'dd-MM-yyyy') AS CancelOn
	,OBD.CancelReason
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CreatedBy) AS CreatedBy
    ,FORMAT(OBD.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
    ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.LastModifiedBy) AS LastModifiedBy
    ,FORMAT(OBD.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
	,ISNULL(FL.FlagId,0) AS FlagId
	,CASE WHEN ISNULL(FL.FlagId,0) > 0 THEN 1 ELSE 0 END AS CanApprove
		,DiscountApprovedID
		,DiscountApprovedName
		,DiscountReason
		,Remark
FROM OPDBookingDetails OBD
inner join PatientMaster PM on pm.PatientId=obd.PatientId
inner join CorporateMaster cm on cm.CorporateId=obd.CorporateId
OUTER APPLY (
	SELECT TOP 1 
		AAM.ApprovalFlowId,
		AAM.IsAllApprovalRequired,
		AAM.ApprovalLevelId,
		AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
	FROM ApprovalAuthorityMaster AAM
	WHERE AAM.ApprovalTypeId = 10
	  AND AAM.RoleId = 0
	  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(10, OBD.BranchId, 0, NULL)
	  AND AAM.IsActive = 1
	  AND AAM.AmountUpTo >= OBD.TotalDiscountAmountOnBill
	ORDER BY AAM.AmountUpTo ASC
) AM
OUTER APPLY (
	SELECT CurLevel = 
		CASE 
			WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
			WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
			WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
			WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
			ELSE 0
		END
) LV
OUTER APPLY (
	SELECT FlagId=
		CASE WHEN LV.CurLevel > 0 THEN
			CASE 
				WHEN (AM.ApprovalFlowId = 1 OR (AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 1)) THEN
					CASE 
						WHEN LV.CurLevel = 1 AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN LV.CurLevel = 2 AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN LV.CurLevel = 3 AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN LV.CurLevel = 4 AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				WHEN AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 0 THEN
					CASE 
						WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				ELSE 0
			END
		ELSE 0
		END
) FL
where obd.IsDiscountApprovalRequired=1
AND OBD.CreatedOn >= @fromDate
AND OBD.CreatedOn < DATEADD(DAY, 1, @toDate)
and (cm.CorporateId=@corporateId or @corporateId=0)
and obd.BranchId=@branchId
order by obd.CreatedOn desc

end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetOPDBookingApprovalDetails]
(
    @BookingId BIGINT
)
AS
BEGIN
    SELECT
         OBD.BookingId
        ,OBD.TokenNo
        ,OBD.BranchId
        ,OBD.PatientId
        ,pm.UHID
        ,pm.PatientName
		,pm.Age
		,pm.Gender
        ,OBD.CorporateId
        ,cm.CorporateName
        ,OBD.TotalBillAmount
        ,OBD.TotalDiscountAmountOnBill
        ,OBD.TotalDiscountPerOnBill                AS BillDiscountPercentage
        ,OBD.TotalApprovedDiscountPerOnBill         AS ApprovedPercentage
		,OBD.TotalPatientPayableAmount
        ,OBD.StatusId
        ,OBD.Status
        ,OBD.IsDiscountApprovalRequired
        ,OBD.IsDiscountApproved

        ,AM.ApprovalFlowId
        ,AM.ApprovalFlow
        ,AM.IsAllApprovalRequired
        ,AM.ApprovalLevelId
        ,AM.ApprovalLevel

        -- Configured approver names per level (comma separated)
        ,dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames

        -- Level 1
        ,OBD.IsLevel1Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level1ApproveId) AS Level1ApprovedByName
        ,FORMAT(OBD.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level1ApproveOn

        -- Level 2
        ,OBD.IsLevel2Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level2ApproveId) AS Level2ApprovedByName
        ,FORMAT(OBD.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level2ApproveOn

        -- Level 3
        ,OBD.IsLevel3Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level3ApproveId) AS Level3ApprovedByName
        ,FORMAT(OBD.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level3ApproveOn

        -- Level 4
        ,OBD.IsLevel4Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level4ApproveId) AS Level4ApprovedByName
        ,FORMAT(OBD.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level4ApproveOn

        -- Next pending approval (name of user list eligible to approve next)
        ,CASE
            WHEN OBD.IsDiscountApproved = 1 THEN NULL
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
            ELSE NULL
         END AS NextApprovalName

        ,CASE
            WHEN OBD.IsDiscountApproved = 1 THEN 0
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
            ELSE 0
         END AS NextApprovalLevel

        ,ISNULL(OBD.ApprovalRemarks,'') AS ApprovalRemarks  
		,DiscountApprovedID
		,DiscountApprovedName
		,DiscountReason
		,Remark

    FROM OPDBookingDetails OBD
    INNER JOIN PatientMaster PM ON pm.PatientId = obd.PatientId
    INNER JOIN CorporateMaster cm ON cm.CorporateId = obd.CorporateId
    OUTER APPLY (
        SELECT TOP 1 *
        FROM ApprovalAuthorityMaster AAM
        WHERE AAM.ApprovalTypeId = 10
          AND AAM.RoleId = 0
          AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(10, OBD.BranchId, 0, NULL)
          AND AAM.IsActive = 1
          AND AAM.AmountUpTo >= OBD.TotalDiscountAmountOnBill
        ORDER BY AAM.AmountUpTo ASC
    ) AM
    WHERE OBD.BookingId = @BookingId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[U_ApproveOPDBookingDiscount]      
@BookingId bigint,      
@flag int,      
@approvedPer decimal(10,6)=0.00,   
@ApprovalRemarks nvarchar(256)=null,
@UserId bigint, 
@IpAddress nvarchar(20)
As    
declare @ApprovalLevelId int,@ApprovalFlowId int,@IsAllApprovalRequired int
declare @TotalDiscountAmountOnBill decimal(16,6), @StatusId int
declare @Level1UserId nvarchar(512), @Level2UserId nvarchar(512), @Level3UserId nvarchar(512), @Level4UserId nvarchar(512)
declare @Level1ApproveId bigint, @Level2ApproveId bigint, @Level3ApproveId bigint, @Level4ApproveId bigint
declare @CurLevel int = 0
declare @CanApprove int = 0
declare @FlagId int = 0

SELECT 
	 @TotalDiscountAmountOnBill = TotalDiscountAmountOnBill
	,@StatusId = StatusId
	,@Level1ApproveId = Level1ApproveId
	,@Level2ApproveId = Level2ApproveId
	,@Level3ApproveId = Level3ApproveId
	,@Level4ApproveId = Level4ApproveId
FROM OPDBookingDetails
WHERE BookingId = @BookingId

-- rules of the request's own branch, else the default (BranchId 0)
declare @RequestBranchId int = (SELECT BranchId FROM OPDBookingDetails WHERE BookingId = @BookingId)

SELECT TOP 1 
	 @ApprovalLevelId = AAM.ApprovalLevelId
	,@ApprovalFlowId = AAM.ApprovalFlowId
	,@IsAllApprovalRequired = AAM.IsAllApprovalRequired
	,@Level1UserId = AAM.Level1UserId
	,@Level2UserId = AAM.Level2UserId
	,@Level3UserId = AAM.Level3UserId
	,@Level4UserId = AAM.Level4UserId
FROM ApprovalAuthorityMaster AAM
WHERE AAM.ApprovalTypeId = 10 
  AND AAM.RoleId = 0
  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(10, @RequestBranchId, 0, NULL)
  AND AAM.IsActive = 1
  AND AAM.AmountUpTo >= @TotalDiscountAmountOnBill
ORDER BY AAM.AmountUpTo ASC

IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL SET @CurLevel = 1
ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL SET @CurLevel = 2
ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL SET @CurLevel = 3
ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL SET @CurLevel = 4
ELSE SET @CurLevel = 0

IF @CurLevel > 0
BEGIN
	IF (@ApprovalFlowId = 1 OR (@ApprovalFlowId = 2 AND @IsAllApprovalRequired = 1))
	BEGIN
		IF @CurLevel = 1 AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @CurLevel = 2 AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @CurLevel = 3 AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @CurLevel = 4 AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
	ELSE IF @ApprovalFlowId = 2 AND @IsAllApprovalRequired = 0
	BEGIN
		IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
END

SET @CanApprove = CASE WHEN @FlagId > 0 THEN 1 ELSE 0 END

Begin      

if(@flag between 1 and 4 and @CanApprove = 1 and @flag = @FlagId)
begin
	if(@flag=1)
	begin
	update OPDBookingDetails     
	set IsLevel1Approve=1
	,TotalApprovedDiscountPerOnBill=@approvedPer
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level1ApproveId=@UserId
	   ,Level1ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where BookingId=@BookingId      
	end
	if(@flag=2)
	begin
	update OPDBookingDetails     
	set IsLevel2Approve=1
	,TotalApprovedDiscountPerOnBill=@approvedPer
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level2ApproveId=@UserId
	   ,Level2ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where BookingId=@BookingId      
	end
	if(@flag=3)
	begin
	update OPDBookingDetails     
	set IsLevel3Approve=1
	,TotalApprovedDiscountPerOnBill=@approvedPer
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level3ApproveId=@UserId
	   ,Level3ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE()
	      ,IpAddress=@IpAddress
	where BookingId=@BookingId      
	end
	if(@flag=4)
	begin
	update OPDBookingDetails     
	set IsLevel4Approve=1
	,TotalApprovedDiscountPerOnBill=@approvedPer
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level4ApproveId=@UserId
	   ,Level4ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where BookingId=@BookingId      
	end
	 
	if(@flag=@ApprovalLevelId or (@IsAllApprovalRequired=0 and @ApprovalFlowId=2))
	begin
	update OPDBookingDetails     
	set Status='Discount Approved'
		,StatusId=2
	,TotalApprovedDiscountPerOnBill=@approvedPer
	,ApprovalRemarks=@ApprovalRemarks
	,IsDiscountApproved=1
	,LastModifiedBy=@UserId
	,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where BookingId=@BookingId      
	end
end
 
end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_OPDRefundRequestDetailsForDiscountApproval]
( @fromDate DATE,
    @toDate   DATE,
	@branchId int,
	@userId int
)
as
begin
SELECT 
	 OBD.RefundId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,OBD.VisitId
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	,OBD.TotalBillAmount
	,OBD.TotalDiscountPerOnBill
	,OBD.TotalDiscountAmountOnBill
	,OBD.RoundOff
	,OBD.TotalRefundAmount
	,OBD.IsRefundApproved
			,OBD.IsRefundCollected
	,OBD.ApprovalRemarks
	,OBD.IsLevel1Approve
	,OBD.Level1ApproveId
	,OBD.Level1ApproveOn
	,OBD.IsLevel2Approve
	,OBD.Level2ApproveId
	,OBD.Level2ApproveOn
	,OBD.IsLevel3Approve
	,OBD.Level3ApproveId
	,OBD.Level3ApproveOn
	,OBD.IsLevel4Approve
	,OBD.Level4ApproveId
	,OBD.Level4ApproveOn
	,OBD.IsCancel
	,OBD.Status
	,OBD.StatusId
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CancelBy) AS CancelBy
    ,FORMAT(OBD.CancelOn, 'dd-MM-yyyy') AS CancelOn
	,OBD.CancelReason
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CreatedBy) AS CreatedBy
    ,FORMAT(OBD.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
    ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.LastModifiedBy) AS LastModifiedBy
    ,FORMAT(OBD.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
	,ISNULL(FL.FlagId,0) AS FlagId
	,CASE WHEN ISNULL(FL.FlagId,0) > 0 THEN 1 ELSE 0 END AS CanApprove
		,RefundApprovedID
		,RefundApprovedName
		,RefundReason
		,RefundRemark
FROM OPDRefundRequestDetails OBD
inner join PatientMaster PM on pm.PatientId=obd.PatientId
OUTER APPLY (
	SELECT TOP 1 
		AAM.ApprovalFlowId,
		AAM.IsAllApprovalRequired,
		AAM.ApprovalLevelId,
		AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
	FROM ApprovalAuthorityMaster AAM
	WHERE AAM.ApprovalTypeId = 9
	  AND AAM.RoleId = 0
	  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(9, OBD.BranchId, 0, NULL)
	  AND AAM.IsActive = 1
	  AND AAM.AmountUpTo >= OBD.TotalRefundAmount
	ORDER BY AAM.AmountUpTo ASC
) AM
OUTER APPLY (
	SELECT CurLevel = 
		CASE 
			WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
			WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
			WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
			WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
			ELSE 0
		END
) LV
OUTER APPLY (
	SELECT FlagId=
		CASE WHEN LV.CurLevel > 0 THEN
			CASE 
				WHEN (AM.ApprovalFlowId = 1 OR (AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 1)) THEN
					CASE 
						WHEN LV.CurLevel = 1 AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN LV.CurLevel = 2 AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN LV.CurLevel = 3 AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN LV.CurLevel = 4 AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				WHEN AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 0 THEN
					CASE 
						WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				ELSE 0
			END
		ELSE 0
		END
) FL
where  OBD.CreatedOn >= @fromDate
AND OBD.CreatedOn < DATEADD(DAY, 1, @toDate)
and obd.BranchId=@branchId
order by obd.CreatedOn desc

end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetOPDRefundRequestApprovalDetails]
(
    @RefundId BIGINT
)
AS
BEGIN
    SELECT
         OBD.RefundId
        ,OBD.TokenNo
        ,OBD.BranchId
        ,OBD.PatientId
		,OBD.VisitId
        ,pm.UHID
        ,pm.PatientName
		,pm.Age
		,pm.Gender
        ,OBD.TotalBillAmount
        ,OBD.TotalDiscountAmountOnBill
        ,OBD.TotalDiscountPerOnBill
		,OBD.TotalRefundAmount
        ,OBD.StatusId
        ,OBD.Status
        ,OBD.IsRefundApproved
				,OBD.IsRefundCollected
        ,AM.ApprovalFlowId
        ,AM.ApprovalFlow
        ,AM.IsAllApprovalRequired
        ,AM.ApprovalLevelId
        ,AM.ApprovalLevel

        -- Configured approver names per level (comma separated)
        ,dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames

        -- Level 1
        ,OBD.IsLevel1Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level1ApproveId) AS Level1ApprovedByName
        ,FORMAT(OBD.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level1ApproveOn

        -- Level 2
        ,OBD.IsLevel2Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level2ApproveId) AS Level2ApprovedByName
        ,FORMAT(OBD.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level2ApproveOn

        -- Level 3
        ,OBD.IsLevel3Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level3ApproveId) AS Level3ApprovedByName
        ,FORMAT(OBD.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level3ApproveOn

        -- Level 4
        ,OBD.IsLevel4Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level4ApproveId) AS Level4ApprovedByName
        ,FORMAT(OBD.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level4ApproveOn

        -- Next pending approval (name of user list eligible to approve next)
        ,CASE
            WHEN OBD.IsRefundApproved = 1 THEN NULL
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
            ELSE NULL
         END AS NextApprovalName

        ,CASE
            WHEN OBD.IsRefundApproved = 1 THEN 0
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
            ELSE 0
         END AS NextApprovalLevel

        ,ISNULL(OBD.ApprovalRemarks,'') AS ApprovalRemarks  
		,RefundApprovedID
		,RefundApprovedName
		,RefundReason
		,RefundRemark

    FROM OPDRefundRequestDetails OBD
    INNER JOIN PatientMaster PM ON pm.PatientId = obd.PatientId
    OUTER APPLY (
        SELECT TOP 1 *
        FROM ApprovalAuthorityMaster AAM
        WHERE AAM.ApprovalTypeId = 9
          AND AAM.RoleId = 0
          AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(9, OBD.BranchId, 0, NULL)
          AND AAM.IsActive = 1
          AND AAM.AmountUpTo >= OBD.TotalRefundAmount
        ORDER BY AAM.AmountUpTo ASC
    ) AM
    WHERE OBD.RefundId = @RefundId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[U_ApproveOPDRefundRequest]      
@RefundId bigint,      
@flag int,      
@ApprovalRemarks nvarchar(256)=null,
@UserId bigint, 
@IpAddress nvarchar(20)
As    
declare @ApprovalLevelId int,@ApprovalFlowId int,@IsAllApprovalRequired int
declare @TotalRefundAmount decimal(16,6), @StatusId int
declare @Level1UserId nvarchar(512), @Level2UserId nvarchar(512), @Level3UserId nvarchar(512), @Level4UserId nvarchar(512)
declare @Level1ApproveId bigint, @Level2ApproveId bigint, @Level3ApproveId bigint, @Level4ApproveId bigint
declare @CurLevel int = 0
declare @CanApprove int = 0
declare @FlagId int = 0

SELECT 
	 @TotalRefundAmount = TotalRefundAmount
	,@StatusId = StatusId
	,@Level1ApproveId = Level1ApproveId
	,@Level2ApproveId = Level2ApproveId
	,@Level3ApproveId = Level3ApproveId
	,@Level4ApproveId = Level4ApproveId
FROM OPDRefundRequestDetails
WHERE RefundId = @RefundId

-- rules of the request's own branch, else the default (BranchId 0)
declare @RequestBranchId int = (SELECT BranchId FROM OPDRefundRequestDetails WHERE RefundId = @RefundId)

SELECT TOP 1 
	 @ApprovalLevelId = AAM.ApprovalLevelId
	,@ApprovalFlowId = AAM.ApprovalFlowId
	,@IsAllApprovalRequired = AAM.IsAllApprovalRequired
	,@Level1UserId = AAM.Level1UserId
	,@Level2UserId = AAM.Level2UserId
	,@Level3UserId = AAM.Level3UserId
	,@Level4UserId = AAM.Level4UserId
FROM ApprovalAuthorityMaster AAM
WHERE AAM.ApprovalTypeId = 9
  AND AAM.RoleId = 0
  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(9, @RequestBranchId, 0, NULL)
  AND AAM.IsActive = 1
  AND AAM.AmountUpTo >= @TotalRefundAmount
ORDER BY AAM.AmountUpTo ASC

IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL SET @CurLevel = 1
ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL SET @CurLevel = 2
ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL SET @CurLevel = 3
ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL SET @CurLevel = 4
ELSE SET @CurLevel = 0

IF @CurLevel > 0
BEGIN
	IF (@ApprovalFlowId = 1 OR (@ApprovalFlowId = 2 AND @IsAllApprovalRequired = 1))
	BEGIN
		IF @CurLevel = 1 AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @CurLevel = 2 AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @CurLevel = 3 AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @CurLevel = 4 AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
	ELSE IF @ApprovalFlowId = 2 AND @IsAllApprovalRequired = 0
	BEGIN
		IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
END

SET @CanApprove = CASE WHEN @FlagId > 0 THEN 1 ELSE 0 END

Begin      

if(@flag between 1 and 4 and @CanApprove = 1 and @flag = @FlagId)
begin
	if(@flag=1)
	begin
	update OPDRefundRequestDetails     
	set IsLevel1Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level1ApproveId=@UserId
	   ,Level1ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where RefundId=@RefundId      
	end
	if(@flag=2)
	begin
	update OPDRefundRequestDetails     
	set IsLevel2Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level2ApproveId=@UserId
	   ,Level2ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where RefundId=@RefundId      
	end
	if(@flag=3)
	begin
	update OPDRefundRequestDetails     
	set IsLevel3Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level3ApproveId=@UserId
	   ,Level3ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE()
	      ,IpAddress=@IpAddress
	where RefundId=@RefundId      
	end
	if(@flag=4)
	begin
	update OPDRefundRequestDetails     
	set IsLevel4Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level4ApproveId=@UserId
	   ,Level4ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where RefundId=@RefundId      
	end
	 
	if(@flag=@ApprovalLevelId or (@IsAllApprovalRequired=0 and @ApprovalFlowId=2))
	begin
	update OPDRefundRequestDetails     
	set Status='Refund Approved'
		,StatusId=1
	,ApprovalRemarks=@ApprovalRemarks
	,IsRefundApproved=1
	,LastModifiedBy=@UserId
	,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where RefundId=@RefundId      
	end
end
 
end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_CreditNoteRequestDetailsForDiscountApproval]
( @fromDate DATE,
    @toDate   DATE,
	@branchId int,
	@userId int
)
as
begin
SELECT 
	 OBD.CreditNoteId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,OBD.VisitId
	,OBD.BillId
   ,(select top 1 pbd.BillNo from PatientBillDetails pbd where pbd.BillId=obd.BillId ) as BillNo
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	
	,OBD.TotalCreditNoteAmount
	,OBD.IsCreditNoteApproved
	,OBD.IsCreditNoteCreated
	,OBD.ApprovalRemarks
	,OBD.IsLevel1Approve
	,OBD.Level1ApproveId
	,OBD.Level1ApproveOn
	,OBD.IsLevel2Approve
	,OBD.Level2ApproveId
	,OBD.Level2ApproveOn
	,OBD.IsLevel3Approve
	,OBD.Level3ApproveId
	,OBD.Level3ApproveOn
	,OBD.IsLevel4Approve
	,OBD.Level4ApproveId
	,OBD.Level4ApproveOn
	,OBD.IsCancel
	,OBD.Status
	,OBD.StatusId
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CancelBy) AS CancelBy
    ,FORMAT(OBD.CancelOn, 'dd-MM-yyyy') AS CancelOn
	,OBD.CancelReason
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CreatedBy) AS CreatedBy
    ,FORMAT(OBD.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
    ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.LastModifiedBy) AS LastModifiedBy
    ,FORMAT(OBD.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
	,ISNULL(FL.FlagId,0) AS FlagId
	,CASE WHEN ISNULL(FL.FlagId,0) > 0 THEN 1 ELSE 0 END AS CanApprove
		,CreditNoteApprovedID
		,CreditNoteApprovedName
		,CreditNoteReason
		,CreditNoteRemark
FROM CreditNoteRequestDetails OBD
inner join PatientMaster PM on pm.PatientId=obd.PatientId
OUTER APPLY (
	SELECT TOP 1 
		AAM.ApprovalFlowId,
		AAM.IsAllApprovalRequired,
		AAM.ApprovalLevelId,
		AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
	FROM ApprovalAuthorityMaster AAM
	WHERE AAM.ApprovalTypeId = 7
	  AND AAM.RoleId = 0
	  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(7, OBD.BranchId, 0, NULL)
	  AND AAM.IsActive = 1
	  AND AAM.AmountUpTo >= OBD.TotalCreditNoteAmount
	ORDER BY AAM.AmountUpTo ASC
) AM
OUTER APPLY (
	SELECT CurLevel = 
		CASE 
			WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
			WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
			WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
			WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
			ELSE 0
		END
) LV
OUTER APPLY (
	SELECT FlagId=
		CASE WHEN LV.CurLevel > 0 THEN
			CASE 
				WHEN (AM.ApprovalFlowId = 1 OR (AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 1)) THEN
					CASE 
						WHEN LV.CurLevel = 1 AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN LV.CurLevel = 2 AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN LV.CurLevel = 3 AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN LV.CurLevel = 4 AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				WHEN AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 0 THEN
					CASE 
						WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				ELSE 0
			END
		ELSE 0
		END
) FL
where  OBD.CreatedOn >= @fromDate
AND OBD.CreatedOn < DATEADD(DAY, 1, @toDate)
and obd.BranchId=@branchId
order by obd.CreatedOn desc

end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetCreditNoteRequestApprovalDetails]
(
    @CreditNoteId BIGINT
)
AS
BEGIN
    SELECT
         OBD.CreditNoteId
        ,OBD.TokenNo
        ,OBD.BranchId
        ,OBD.PatientId
		,OBD.VisitId
		,OBD.BillId
		,PBD.BillNo
        ,pm.UHID
        ,pm.PatientName
		,pm.Age
		,pm.Gender
        ,PBD.TotalBillAmount
        ,PBD.TotalDiscountAmountOnBill
        ,PBD.TotalDiscountPerOnBill
        ,PBD.TotalPaidAmount
        ,PBD.TotalBalanceAmount
		,OBD.TotalCreditNoteAmount
        ,OBD.StatusId
        ,OBD.Status
        ,OBD.IsCreditNoteApproved

        ,AM.ApprovalFlowId
        ,AM.ApprovalFlow
        ,AM.IsAllApprovalRequired
        ,AM.ApprovalLevelId
        ,AM.ApprovalLevel

        -- Configured approver names per level (comma separated)
        ,dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames

        -- Level 1
        ,OBD.IsLevel1Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level1ApproveId) AS Level1ApprovedByName
        ,FORMAT(OBD.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level1ApproveOn

        -- Level 2
        ,OBD.IsLevel2Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level2ApproveId) AS Level2ApprovedByName
        ,FORMAT(OBD.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level2ApproveOn

        -- Level 3
        ,OBD.IsLevel3Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level3ApproveId) AS Level3ApprovedByName
        ,FORMAT(OBD.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level3ApproveOn

        -- Level 4
        ,OBD.IsLevel4Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level4ApproveId) AS Level4ApprovedByName
        ,FORMAT(OBD.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level4ApproveOn

        -- Next pending approval (name of user list eligible to approve next)
        ,CASE
            WHEN OBD.IsCreditNoteApproved = 1 THEN NULL
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
            ELSE NULL
         END AS NextApprovalName

        ,CASE
            WHEN OBD.IsCreditNoteApproved = 1 THEN 0
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
            ELSE 0
         END AS NextApprovalLevel

        ,ISNULL(OBD.ApprovalRemarks,'') AS ApprovalRemarks  
		,CreditNoteApprovedID
		,CreditNoteApprovedName
		,CreditNoteReason
		,CreditNoteRemark

    FROM CreditNoteRequestDetails OBD
    INNER JOIN PatientMaster PM ON pm.PatientId = obd.PatientId
    INNER JOIN PatientBillDetails PBD ON PBD.BillId = obd.BillId
    OUTER APPLY (
        SELECT TOP 1 *
        FROM ApprovalAuthorityMaster AAM
        WHERE AAM.ApprovalTypeId = 7
          AND AAM.RoleId = 0
          AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(7, OBD.BranchId, 0, NULL)
          AND AAM.IsActive = 1
          AND AAM.AmountUpTo >= OBD.TotalCreditNoteAmount
        ORDER BY AAM.AmountUpTo ASC
    ) AM
    WHERE OBD.CreditNoteId = @CreditNoteId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[U_ApproveCreditNoteRequest]      
@CreditNoteId bigint,      
@flag int,      
@ApprovalRemarks nvarchar(256)=null,
@UserId bigint, 
@IpAddress nvarchar(20)
As    
declare @ApprovalLevelId int,@ApprovalFlowId int,@IsAllApprovalRequired int
declare @TotalCreditNoteAmount decimal(16,6), @StatusId int
declare @Level1UserId nvarchar(512), @Level2UserId nvarchar(512), @Level3UserId nvarchar(512), @Level4UserId nvarchar(512)
declare @Level1ApproveId bigint, @Level2ApproveId bigint, @Level3ApproveId bigint, @Level4ApproveId bigint
declare @CurLevel int = 0
declare @CanApprove int = 0
declare @FlagId int = 0

SELECT 
	 @TotalCreditNoteAmount = TotalCreditNoteAmount
	,@StatusId = StatusId
	,@Level1ApproveId = Level1ApproveId
	,@Level2ApproveId = Level2ApproveId
	,@Level3ApproveId = Level3ApproveId
	,@Level4ApproveId = Level4ApproveId
FROM CreditNoteRequestDetails
WHERE CreditNoteId = @CreditNoteId

-- rules of the request's own branch, else the default (BranchId 0)
declare @RequestBranchId int = (SELECT BranchId FROM CreditNoteRequestDetails WHERE CreditNoteId = @CreditNoteId)

SELECT TOP 1 
	 @ApprovalLevelId = AAM.ApprovalLevelId
	,@ApprovalFlowId = AAM.ApprovalFlowId
	,@IsAllApprovalRequired = AAM.IsAllApprovalRequired
	,@Level1UserId = AAM.Level1UserId
	,@Level2UserId = AAM.Level2UserId
	,@Level3UserId = AAM.Level3UserId
	,@Level4UserId = AAM.Level4UserId
FROM ApprovalAuthorityMaster AAM
WHERE AAM.ApprovalTypeId = 7
  AND AAM.RoleId = 0
  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(7, @RequestBranchId, 0, NULL)
  AND AAM.IsActive = 1
  AND AAM.AmountUpTo >= @TotalCreditNoteAmount
ORDER BY AAM.AmountUpTo ASC

IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL SET @CurLevel = 1
ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL SET @CurLevel = 2
ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL SET @CurLevel = 3
ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL SET @CurLevel = 4
ELSE SET @CurLevel = 0

IF @CurLevel > 0
BEGIN
	IF (@ApprovalFlowId = 1 OR (@ApprovalFlowId = 2 AND @IsAllApprovalRequired = 1))
	BEGIN
		IF @CurLevel = 1 AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @CurLevel = 2 AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @CurLevel = 3 AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @CurLevel = 4 AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
	ELSE IF @ApprovalFlowId = 2 AND @IsAllApprovalRequired = 0
	BEGIN
		IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
END

SET @CanApprove = CASE WHEN @FlagId > 0 THEN 1 ELSE 0 END

Begin      

if(@flag between 1 and 4 and @CanApprove = 1 and @flag = @FlagId)
begin
	if(@flag=1)
	begin
	update CreditNoteRequestDetails     
	set IsLevel1Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level1ApproveId=@UserId
	   ,Level1ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where CreditNoteId=@CreditNoteId      
	end
	if(@flag=2)
	begin
	update CreditNoteRequestDetails     
	set IsLevel2Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level2ApproveId=@UserId
	   ,Level2ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where CreditNoteId=@CreditNoteId      
	end
	if(@flag=3)
	begin
	update CreditNoteRequestDetails     
	set IsLevel3Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level3ApproveId=@UserId
	   ,Level3ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE()
	      ,IpAddress=@IpAddress
	where CreditNoteId=@CreditNoteId      
	end
	if(@flag=4)
	begin
	update CreditNoteRequestDetails     
	set IsLevel4Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level4ApproveId=@UserId
	   ,Level4ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where CreditNoteId=@CreditNoteId      
	end
	 
	if(@flag=@ApprovalLevelId or (@IsAllApprovalRequired=0 and @ApprovalFlowId=2))
	begin
	update CreditNoteRequestDetails     
	set Status='Credit Note Approved'
		,StatusId=1
	,ApprovalRemarks=@ApprovalRemarks
	,IsCreditNoteApproved=1
	,LastModifiedBy=@UserId
	,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where CreditNoteId=@CreditNoteId      
	end
end
 
end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_WriteOffRequestDetailsForDiscountApproval]
( @fromDate DATE,
    @toDate   DATE,
	@branchId int,
	@userId int
)
as
begin
SELECT 
	 OBD.WriteOffId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,OBD.VisitId
	,OBD.BillId
	,(select top 1 pbd.BillNo from PatientBillDetails pbd where pbd.BillId=obd.BillId ) as BillNo
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	
	,OBD.TotalWriteOffAmount
	,OBD.IsWriteOffApproved
	,OBD.IsWriteOffCreated
	,OBD.ApprovalRemarks
	,OBD.IsLevel1Approve
	,OBD.Level1ApproveId
	,OBD.Level1ApproveOn
	,OBD.IsLevel2Approve
	,OBD.Level2ApproveId
	,OBD.Level2ApproveOn
	,OBD.IsLevel3Approve
	,OBD.Level3ApproveId
	,OBD.Level3ApproveOn
	,OBD.IsLevel4Approve
	,OBD.Level4ApproveId
	,OBD.Level4ApproveOn
	,OBD.IsCancel
	,OBD.Status
	,OBD.StatusId
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CancelBy) AS CancelBy
    ,FORMAT(OBD.CancelOn, 'dd-MM-yyyy') AS CancelOn
	,OBD.CancelReason
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CreatedBy) AS CreatedBy
    ,FORMAT(OBD.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
    ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.LastModifiedBy) AS LastModifiedBy
    ,FORMAT(OBD.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
	,ISNULL(FL.FlagId,0) AS FlagId
	,CASE WHEN ISNULL(FL.FlagId,0) > 0 THEN 1 ELSE 0 END AS CanApprove
		,WriteOffApprovedID
		,WriteOffApprovedName
		,WriteOffReason
		,WriteOffRemark
FROM WriteOffRequestDetails OBD
inner join PatientMaster PM on pm.PatientId=obd.PatientId
OUTER APPLY (
	SELECT TOP 1 
		AAM.ApprovalFlowId,
		AAM.IsAllApprovalRequired,
		AAM.ApprovalLevelId,
		AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
	FROM ApprovalAuthorityMaster AAM
	WHERE AAM.ApprovalTypeId = 8
	  AND AAM.RoleId = 0
	  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(8, OBD.BranchId, 0, NULL)
	  AND AAM.IsActive = 1
	  AND AAM.AmountUpTo >= OBD.TotalWriteOffAmount
	ORDER BY AAM.AmountUpTo ASC
) AM
OUTER APPLY (
	SELECT CurLevel = 
		CASE 
			WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
			WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
			WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
			WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
			ELSE 0
		END
) LV
OUTER APPLY (
	SELECT FlagId=
		CASE WHEN LV.CurLevel > 0 THEN
			CASE 
				WHEN (AM.ApprovalFlowId = 1 OR (AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 1)) THEN
					CASE 
						WHEN LV.CurLevel = 1 AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN LV.CurLevel = 2 AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN LV.CurLevel = 3 AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN LV.CurLevel = 4 AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				WHEN AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 0 THEN
					CASE 
						WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				ELSE 0
			END
		ELSE 0
		END
) FL
where  OBD.CreatedOn >= @fromDate
AND OBD.CreatedOn < DATEADD(DAY, 1, @toDate)
and obd.BranchId=@branchId
order by obd.CreatedOn desc

end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetWriteOffRequestApprovalDetails]
(
    @WriteOffId BIGINT
)
AS
BEGIN
    SELECT
         OBD.WriteOffId
        ,OBD.TokenNo
        ,OBD.BranchId
        ,OBD.PatientId
		,OBD.VisitId
		,OBD.BillId
		,PBD.BillNo
        ,pm.UHID
        ,pm.PatientName
		,pm.Age
		,pm.Gender
        ,PBD.TotalBillAmount
        ,PBD.TotalDiscountAmountOnBill
        ,PBD.TotalDiscountPerOnBill
        ,PBD.TotalPaidAmount
        ,PBD.TotalBalanceAmount
		,OBD.TotalWriteOffAmount
        ,OBD.StatusId
        ,OBD.Status
        ,OBD.IsWriteOffApproved

        ,AM.ApprovalFlowId
        ,AM.ApprovalFlow
        ,AM.IsAllApprovalRequired
        ,AM.ApprovalLevelId
        ,AM.ApprovalLevel

        -- Configured approver names per level (comma separated)
        ,dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames

        -- Level 1
        ,OBD.IsLevel1Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level1ApproveId) AS Level1ApprovedByName
        ,FORMAT(OBD.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level1ApproveOn

        -- Level 2
        ,OBD.IsLevel2Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level2ApproveId) AS Level2ApprovedByName
        ,FORMAT(OBD.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level2ApproveOn

        -- Level 3
        ,OBD.IsLevel3Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level3ApproveId) AS Level3ApprovedByName
        ,FORMAT(OBD.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level3ApproveOn

        -- Level 4
        ,OBD.IsLevel4Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level4ApproveId) AS Level4ApprovedByName
        ,FORMAT(OBD.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level4ApproveOn

        -- Next pending approval (name of user list eligible to approve next)
        ,CASE
            WHEN OBD.IsWriteOffApproved = 1 THEN NULL
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
            ELSE NULL
         END AS NextApprovalName

        ,CASE
            WHEN OBD.IsWriteOffApproved = 1 THEN 0
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
            ELSE 0
         END AS NextApprovalLevel

        ,ISNULL(OBD.ApprovalRemarks,'') AS ApprovalRemarks  
		,WriteOffApprovedID
		,WriteOffApprovedName
		,WriteOffReason
		,WriteOffRemark

    FROM WriteOffRequestDetails OBD
    INNER JOIN PatientMaster PM ON pm.PatientId = obd.PatientId
    INNER JOIN PatientBillDetails PBD ON PBD.BillId = obd.BillId
    OUTER APPLY (
        SELECT TOP 1 *
        FROM ApprovalAuthorityMaster AAM
        WHERE AAM.ApprovalTypeId = 8
          AND AAM.RoleId = 0
          AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(8, OBD.BranchId, 0, NULL)
          AND AAM.IsActive = 1
          AND AAM.AmountUpTo >= OBD.TotalWriteOffAmount
        ORDER BY AAM.AmountUpTo ASC
    ) AM
    WHERE OBD.WriteOffId = @WriteOffId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[U_ApproveWriteOffRequest]      
@WriteOffId bigint,      
@flag int,      
@ApprovalRemarks nvarchar(256)=null,
@UserId bigint, 
@IpAddress nvarchar(20)
As    
declare @ApprovalLevelId int,@ApprovalFlowId int,@IsAllApprovalRequired int
declare @TotalWriteOffAmount decimal(16,6), @StatusId int
declare @Level1UserId nvarchar(512), @Level2UserId nvarchar(512), @Level3UserId nvarchar(512), @Level4UserId nvarchar(512)
declare @Level1ApproveId bigint, @Level2ApproveId bigint, @Level3ApproveId bigint, @Level4ApproveId bigint
declare @CurLevel int = 0
declare @CanApprove int = 0
declare @FlagId int = 0

SELECT 
	 @TotalWriteOffAmount = TotalWriteOffAmount
	,@StatusId = StatusId
	,@Level1ApproveId = Level1ApproveId
	,@Level2ApproveId = Level2ApproveId
	,@Level3ApproveId = Level3ApproveId
	,@Level4ApproveId = Level4ApproveId
FROM WriteOffRequestDetails
WHERE WriteOffId = @WriteOffId

-- rules of the request's own branch, else the default (BranchId 0)
declare @RequestBranchId int = (SELECT BranchId FROM WriteOffRequestDetails WHERE WriteOffId = @WriteOffId)

SELECT TOP 1 
	 @ApprovalLevelId = AAM.ApprovalLevelId
	,@ApprovalFlowId = AAM.ApprovalFlowId
	,@IsAllApprovalRequired = AAM.IsAllApprovalRequired
	,@Level1UserId = AAM.Level1UserId
	,@Level2UserId = AAM.Level2UserId
	,@Level3UserId = AAM.Level3UserId
	,@Level4UserId = AAM.Level4UserId
FROM ApprovalAuthorityMaster AAM
WHERE AAM.ApprovalTypeId = 8
  AND AAM.RoleId = 0
  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(8, @RequestBranchId, 0, NULL)
  AND AAM.IsActive = 1
  AND AAM.AmountUpTo >= @TotalWriteOffAmount
ORDER BY AAM.AmountUpTo ASC

IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL SET @CurLevel = 1
ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL SET @CurLevel = 2
ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL SET @CurLevel = 3
ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL SET @CurLevel = 4
ELSE SET @CurLevel = 0

IF @CurLevel > 0
BEGIN
	IF (@ApprovalFlowId = 1 OR (@ApprovalFlowId = 2 AND @IsAllApprovalRequired = 1))
	BEGIN
		IF @CurLevel = 1 AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @CurLevel = 2 AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @CurLevel = 3 AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @CurLevel = 4 AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
	ELSE IF @ApprovalFlowId = 2 AND @IsAllApprovalRequired = 0
	BEGIN
		IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
END

SET @CanApprove = CASE WHEN @FlagId > 0 THEN 1 ELSE 0 END

Begin      

if(@flag between 1 and 4 and @CanApprove = 1 and @flag = @FlagId)
begin
	if(@flag=1)
	begin
	update WriteOffRequestDetails     
	set IsLevel1Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level1ApproveId=@UserId
	   ,Level1ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where WriteOffId=@WriteOffId      
	end
	if(@flag=2)
	begin
	update WriteOffRequestDetails     
	set IsLevel2Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level2ApproveId=@UserId
	   ,Level2ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where WriteOffId=@WriteOffId      
	end
	if(@flag=3)
	begin
	update WriteOffRequestDetails     
	set IsLevel3Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level3ApproveId=@UserId
	   ,Level3ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE()
	      ,IpAddress=@IpAddress
	where WriteOffId=@WriteOffId      
	end
	if(@flag=4)
	begin
	update WriteOffRequestDetails     
	set IsLevel4Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level4ApproveId=@UserId
	   ,Level4ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where WriteOffId=@WriteOffId      
	end
	 
	if(@flag=@ApprovalLevelId or (@IsAllApprovalRequired=0 and @ApprovalFlowId=2))
	begin
	update WriteOffRequestDetails     
	set Status='WriteOff Approved'
		,StatusId=1
	,ApprovalRemarks=@ApprovalRemarks
	,IsWriteOffApproved=1
	,LastModifiedBy=@UserId
	,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where WriteOffId=@WriteOffId      
	end
end
 
end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_CorporateTransferRequestDetailsForApproval]
( @fromDate DATE,
    @toDate   DATE,
	@branchId int,
	@userId int
)
as
begin
SELECT 
	 OBD.CorporateTransferId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,OBD.VisitId
	,OBD.TypeId
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	,OBD.TypeId
	,OBD.InsuranceCompanyId
	,isnull((select ic.InsuranceCompanyName from InsuranceCompanyMaster IC where ic.InsuranceCompanyId=obd.InsuranceCompanyId),'Self') as InsuranceCompanyName
	,OBD.CorporateId
	,(select cm.CorporateName from CorporateMaster cm where cm.CorporateId=obd.CorporateId) as CorporateName
	,OBD.BillingTypeId
	,OBD.IsChangeTariff
	,OBD.ChangeFromDate
	,OBD.ChangeToDate
	,OBD.Relation
	,OBD.RelativeName
	,OBD.CardNo
	,isnull(Format(OBD.TransferDate,'dd-MM-yyyy'),'') TransferDate   
          ,OBD.AuthorizationNumber
          ,OBD.ReasonForTransfer
          ,OBD.Remarks
		  ,OBD.IsCorporateTransferCreated
	,OBD.IsCorporateTransferApproved
	,OBD.ApprovalRemarks
	,OBD.IsLevel1Approve
	,OBD.Level1ApproveId
	,OBD.Level1ApproveOn
	,OBD.IsLevel2Approve
	,OBD.Level2ApproveId
	,OBD.Level2ApproveOn
	,OBD.IsLevel3Approve
	,OBD.Level3ApproveId
	,OBD.Level3ApproveOn
	,OBD.IsLevel4Approve
	,OBD.Level4ApproveId
	,OBD.Level4ApproveOn
	,OBD.IsCancel
	,OBD.Status
	,OBD.StatusId
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CancelBy) AS CancelBy
    ,FORMAT(OBD.CancelOn, 'dd-MM-yyyy') AS CancelOn
	,OBD.CancelReason
	,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.CreatedBy) AS CreatedBy
    ,FORMAT(OBD.CreatedOn, 'dd-MM-yyyy') AS CreatedOn
    ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = OBD.LastModifiedBy) AS LastModifiedBy
    ,FORMAT(OBD.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
	,ISNULL(FL.FlagId,0) AS FlagId
	,CASE WHEN ISNULL(FL.FlagId,0) > 0 THEN 1 ELSE 0 END AS CanApprove
		
FROM CorporateTransferRequestDetails OBD
inner join PatientMaster PM on pm.PatientId=obd.PatientId
OUTER APPLY (
	SELECT TOP 1 
		AAM.ApprovalFlowId,
		AAM.IsAllApprovalRequired,
		AAM.ApprovalLevelId,
		AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
	FROM ApprovalAuthorityMaster AAM
	WHERE AAM.ApprovalTypeId = 6
	  AND AAM.RoleId = 0
	  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(6, OBD.BranchId, 0, NULL)
	  AND AAM.IsActive = 1
	ORDER BY AAM.AmountUpTo ASC
) AM
OUTER APPLY (
	SELECT CurLevel = 
		CASE 
			WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
			WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
			WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
			WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
			ELSE 0
		END
) LV
OUTER APPLY (
	SELECT FlagId=
		CASE WHEN LV.CurLevel > 0 THEN
			CASE 
				WHEN (AM.ApprovalFlowId = 1 OR (AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 1)) THEN
					CASE 
						WHEN LV.CurLevel = 1 AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN LV.CurLevel = 2 AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN LV.CurLevel = 3 AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN LV.CurLevel = 4 AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				WHEN AM.ApprovalFlowId = 2 AND AM.IsAllApprovalRequired = 0 THEN
					CASE 
						WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level1UserId, @userId) = 1 THEN 1
						WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level2UserId, @userId) = 1 THEN 2
						WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level3UserId, @userId) = 1 THEN 3
						WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL AND dbo.fn_IsUserInList(AM.Level4UserId, @userId) = 1 THEN 4
						ELSE 0
					END
				ELSE 0
			END
		ELSE 0
		END
) FL
where  OBD.CreatedOn >= @fromDate
AND OBD.CreatedOn < DATEADD(DAY, 1, @toDate)
and obd.BranchId=@branchId
order by obd.CreatedOn desc

end
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetCorporateTransferRequestApprovalDetails]
(
    @CorporateTransferId BIGINT
)
AS
BEGIN
    SELECT
         OBD.CorporateTransferId
	,OBD.TokenNo
	,OBD.BranchId
	,OBD.PatientId
	,OBD.VisitId
	,OBD.TypeId
	,pm.UHID
	,pm.PatientName
	,pm.Age
	,pm.Gender
	,OBD.TypeId
	,OBD.InsuranceCompanyId
	,isnull((select ic.InsuranceCompanyName from InsuranceCompanyMaster IC where ic.InsuranceCompanyId=obd.InsuranceCompanyId),'Self') as InsuranceCompanyName
	,OBD.CorporateId
	,(select cm.CorporateName from CorporateMaster cm where cm.CorporateId=obd.CorporateId) as CorporateName
	,OBD.BillingTypeId
	,OBD.IsChangeTariff
	,OBD.ChangeFromDate
	,OBD.ChangeToDate
	,OBD.Relation
	,OBD.RelativeName
	,OBD.CardNo
	,isnull(Format(OBD.TransferDate,'dd-MM-yyyy'),'') TransferDate   
          ,OBD.AuthorizationNumber
          ,OBD.ReasonForTransfer
          ,OBD.Remarks
        ,OBD.StatusId
        ,OBD.Status
		,OBD.IsCorporateTransferCreated
        ,OBD.IsCorporateTransferApproved

        ,AM.ApprovalFlowId
        ,AM.ApprovalFlow
        ,AM.IsAllApprovalRequired
        ,AM.ApprovalLevelId
        ,AM.ApprovalLevel

        -- Configured approver names per level (comma separated)
        ,dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames
        ,dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames

        -- Level 1
        ,OBD.IsLevel1Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level1ApproveId) AS Level1ApprovedByName
        ,FORMAT(OBD.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level1ApproveOn

        -- Level 2
        ,OBD.IsLevel2Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level2ApproveId) AS Level2ApprovedByName
        ,FORMAT(OBD.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level2ApproveOn

        -- Level 3
        ,OBD.IsLevel3Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level3ApproveId) AS Level3ApprovedByName
        ,FORMAT(OBD.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level3ApproveOn

        -- Level 4
        ,OBD.IsLevel4Approve
        ,(SELECT TOP 1 name FROM UserMaster WHERE id = OBD.Level4ApproveId) AS Level4ApprovedByName
        ,FORMAT(OBD.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt')                 AS Level4ApproveOn

        -- Next pending approval (name of user list eligible to approve next)
        ,CASE
            WHEN OBD.IsCorporateTransferApproved = 1 THEN NULL
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
            ELSE NULL
         END AS NextApprovalName

        ,CASE
            WHEN OBD.IsCorporateTransferApproved = 1 THEN 0
            WHEN AM.ApprovalLevelId >= 1 AND OBD.Level1ApproveId IS NULL THEN 1
            WHEN AM.ApprovalLevelId >= 2 AND OBD.Level2ApproveId IS NULL THEN 2
            WHEN AM.ApprovalLevelId >= 3 AND OBD.Level3ApproveId IS NULL THEN 3
            WHEN AM.ApprovalLevelId >= 4 AND OBD.Level4ApproveId IS NULL THEN 4
            ELSE 0
         END AS NextApprovalLevel

        ,ISNULL(OBD.ApprovalRemarks,'') AS ApprovalRemarks  
		

    FROM CorporateTransferRequestDetails OBD
    INNER JOIN PatientMaster PM ON pm.PatientId = obd.PatientId
    OUTER APPLY (
        SELECT TOP 1 *
        FROM ApprovalAuthorityMaster AAM
        WHERE AAM.ApprovalTypeId = 6
          AND AAM.RoleId = 0
          AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(6, OBD.BranchId, 0, NULL)
          AND AAM.IsActive = 1
        ORDER BY AAM.AmountUpTo ASC
    ) AM
    WHERE OBD.CorporateTransferId = @CorporateTransferId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[U_ApproveCorporateTransferRequest]      
@CorporateTransferId bigint,      
@flag int,      
@ApprovalRemarks nvarchar(256)=null,
@UserId bigint, 
@IpAddress nvarchar(20)
As    
declare @ApprovalLevelId int,@ApprovalFlowId int,@IsAllApprovalRequired int
declare @StatusId int
declare @Level1UserId nvarchar(512), @Level2UserId nvarchar(512), @Level3UserId nvarchar(512), @Level4UserId nvarchar(512)
declare @Level1ApproveId bigint, @Level2ApproveId bigint, @Level3ApproveId bigint, @Level4ApproveId bigint
declare @CurLevel int = 0
declare @CanApprove int = 0
declare @FlagId int = 0

SELECT 
	 @StatusId = StatusId
	,@Level1ApproveId = Level1ApproveId
	,@Level2ApproveId = Level2ApproveId
	,@Level3ApproveId = Level3ApproveId
	,@Level4ApproveId = Level4ApproveId
FROM CorporateTransferRequestDetails
WHERE CorporateTransferId = @CorporateTransferId

-- rules of the request's own branch, else the default (BranchId 0)
declare @RequestBranchId int = (SELECT BranchId FROM CorporateTransferRequestDetails WHERE CorporateTransferId = @CorporateTransferId)

SELECT TOP 1 
	 @ApprovalLevelId = AAM.ApprovalLevelId
	,@ApprovalFlowId = AAM.ApprovalFlowId
	,@IsAllApprovalRequired = AAM.IsAllApprovalRequired
	,@Level1UserId = AAM.Level1UserId
	,@Level2UserId = AAM.Level2UserId
	,@Level3UserId = AAM.Level3UserId
	,@Level4UserId = AAM.Level4UserId
FROM ApprovalAuthorityMaster AAM
WHERE AAM.ApprovalTypeId = 6
  AND AAM.RoleId = 0
  AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(6, @RequestBranchId, 0, NULL)
  AND AAM.IsActive = 1
ORDER BY AAM.AmountUpTo ASC

IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL SET @CurLevel = 1
ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL SET @CurLevel = 2
ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL SET @CurLevel = 3
ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL SET @CurLevel = 4
ELSE SET @CurLevel = 0

IF @CurLevel > 0
BEGIN
	IF (@ApprovalFlowId = 1 OR (@ApprovalFlowId = 2 AND @IsAllApprovalRequired = 1))
	BEGIN
		IF @CurLevel = 1 AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @CurLevel = 2 AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @CurLevel = 3 AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @CurLevel = 4 AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
	ELSE IF @ApprovalFlowId = 2 AND @IsAllApprovalRequired = 0
	BEGIN
		IF @ApprovalLevelId >= 1 AND @Level1ApproveId IS NULL AND dbo.fn_IsUserInList(@Level1UserId, @UserId) = 1 SET @FlagId = 1
		ELSE IF @ApprovalLevelId >= 2 AND @Level2ApproveId IS NULL AND dbo.fn_IsUserInList(@Level2UserId, @UserId) = 1 SET @FlagId = 2
		ELSE IF @ApprovalLevelId >= 3 AND @Level3ApproveId IS NULL AND dbo.fn_IsUserInList(@Level3UserId, @UserId) = 1 SET @FlagId = 3
		ELSE IF @ApprovalLevelId >= 4 AND @Level4ApproveId IS NULL AND dbo.fn_IsUserInList(@Level4UserId, @UserId) = 1 SET @FlagId = 4
	END
END

SET @CanApprove = CASE WHEN @FlagId > 0 THEN 1 ELSE 0 END

Begin      

if(@flag between 1 and 4 and @CanApprove = 1 and @flag = @FlagId)
begin
	if(@flag=1)
	begin
	update CorporateTransferRequestDetails     
	set IsLevel1Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level1ApproveId=@UserId
	   ,Level1ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where CorporateTransferId=@CorporateTransferId      
	end
	if(@flag=2)
	begin
	update CorporateTransferRequestDetails     
	set IsLevel2Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level2ApproveId=@UserId
	   ,Level2ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where CorporateTransferId=@CorporateTransferId      
	end
	if(@flag=3)
	begin
	update CorporateTransferRequestDetails     
	set IsLevel3Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level3ApproveId=@UserId
	   ,Level3ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE()
	      ,IpAddress=@IpAddress
	where CorporateTransferId=@CorporateTransferId      
	end
	if(@flag=4)
	begin
	update CorporateTransferRequestDetails     
	set IsLevel4Approve=1
	,ApprovalRemarks=@ApprovalRemarks
	   ,Level4ApproveId=@UserId
	   ,Level4ApproveOn=GETDATE() 
	   ,LastModifiedBy=@UserId
	   ,LastModifiedOn=GETDATE() 
	      ,IpAddress=@IpAddress
	where CorporateTransferId=@CorporateTransferId      
	end
	 
	if(@flag=@ApprovalLevelId or (@IsAllApprovalRequired=0 and @ApprovalFlowId=2))
	begin
	update CorporateTransferRequestDetails     
	set Status='Corporate Transfer Approved'
		,StatusId=1
	,ApprovalRemarks=@ApprovalRemarks
	,IsCorporateTransferApproved=1
	,LastModifiedBy=@UserId
	,LastModifiedOn=GETDATE() 
	   ,IpAddress=@IpAddress
	where CorporateTransferId=@CorporateTransferId      
	end
end
 
end
GO

/* approval rule of a request by amount (write-off pattern: smallest AmountUpTo >= amount) */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseRequestApprovalRule (@purchaseRequestId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 AAM.Id AS ApprovalAuthorityId, AAM.ApprovalFlowId, AAM.ApprovalFlow, AAM.IsAllApprovalRequired,
           AAM.ApprovalLevelId, AAM.ApprovalLevel, AAM.AmountUpTo,
           AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
    FROM dbo.PurchaseRequest PR
    INNER JOIN dbo.ApprovalAuthorityMaster AAM
            ON AAM.ApprovalTypeId = 2 AND AAM.IsActive = 1 AND AAM.HospId = PR.HospId
           AND AAM.RoleId IN (PR.FromDeptId, 0)
           AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(2, PR.BranchId, PR.FromDeptId, PR.HospId)
           AND ISNULL(AAM.AmountUpTo, 0) > 0
           AND AAM.AmountUpTo >= ISNULL(PR.ApprovalAmount, PR.TotalAmount)
    WHERE PR.PurchaseRequestId = @purchaseRequestId
    ORDER BY CASE WHEN AAM.RoleId = PR.FromDeptId THEN 0 ELSE 1 END,
             AAM.AmountUpTo ASC,
             AAM.Id DESC
);
GO

/* approval rule of a PO by amount: type 3, From Department role before 0, branch rules else default branch 0, smallest AmountUpTo */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseOrderApprovalRule (@poId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 AAM.Id AS ApprovalAuthorityId, AAM.ApprovalFlowId, AAM.ApprovalFlow, AAM.IsAllApprovalRequired,
           AAM.ApprovalLevelId, AAM.ApprovalLevel, AAM.AmountUpTo,
           AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
    FROM dbo.PurchaseOrder PO
    INNER JOIN dbo.ApprovalAuthorityMaster AAM
            ON AAM.ApprovalTypeId = 3 AND AAM.IsActive = 1 AND AAM.HospId = PO.HospId
           AND AAM.RoleId IN (PO.RoleId, 0)
           AND AAM.BranchId = dbo.fn_ApprovalRuleBranch(3, PO.BranchId, PO.RoleId, PO.HospId)
           AND ISNULL(AAM.AmountUpTo, 0) > 0
           AND AAM.AmountUpTo >= ISNULL(PO.ApprovalAmount, PO.NetAmount)
    WHERE PO.PoId = @poId
    ORDER BY CASE WHEN AAM.RoleId = PO.RoleId THEN 0 ELSE 1 END,
             AAM.AmountUpTo ASC,
             AAM.Id DESC
);
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetApprovalAuthorityMasterList]  
(  
  @approvalTypeId int  
   
)  
 as   
 begin  
 select  
    Id  
	   ,ISNULL(aam.BranchId, 0) AS BranchId
	   ,CASE WHEN ISNULL(aam.BranchId, 0) = 0 THEN 'Default'
	         ELSE (SELECT TOP 1 BranchName FROM BranchMaster bm WHERE bm.BranchId = aam.BranchId) END AS BranchName
	   ,ApprovalFlowId  
   ,ApprovalFlow  
   ,IsAllApprovalRequired  
      ,ApprovalTypeId  
      ,ApprovalType  
      ,RoleId  
      ,isnull((select RoleName from RoleMaster rm where rm.RoleId=aam.RoleId),'All') as RoleName  

      ,ISNULL((SELECT STRING_AGG(um.Name, ', ') FROM UserMaster um WHERE um.Id IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(aam.Level1UserId, ','))), '') AS Level1UserName
      ,ISNULL((SELECT STRING_AGG(um.Name, ', ') FROM UserMaster um WHERE um.Id IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(aam.Level2UserId, ','))), '') AS Level2UserName
      ,ISNULL((SELECT STRING_AGG(um.Name, ', ') FROM UserMaster um WHERE um.Id IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(aam.Level3UserId, ','))), '') AS Level3UserName
      ,ISNULL((SELECT STRING_AGG(um.Name, ', ') FROM UserMaster um WHERE um.Id IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(aam.Level4UserId, ','))), '') AS Level4UserName 

      ,ApprovalLevelId  
      ,ApprovalLevel  
      ,Level1UserId  
      ,Level2UserId  
      ,Level3UserId  
      ,Level4UserId  
      ,AmountUpTo
	  ,IsActive  
              ,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = AAM.CreatedBy) AS CreatedBy,
        FORMAT(AAM.CreatedOn, 'dd-MM-yyyy') AS CreatedOn,
        (SELECT TOP 1 name FROM UserMaster um WHERE um.id = AAM.LastModifiedBy) AS LastModifiedBy,
        FORMAT(AAM.LastModifiedDate, 'dd-MM-yyyy') AS LastModifiedOn
  
  FROM ApprovalAuthorityMaster AAM  
  where ApprovalTypeId=@approvalTypeId;  
 end
GO
