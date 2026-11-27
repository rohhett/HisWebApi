/* =====================================================================
   Rename "Sample Segregation" -> "Sent To Department"  â€”  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (column renames are guarded; procedures use CREATE OR ALTER;
                old procedure dropped with DROP ... IF EXISTS; menu UPDATE is idempotent)

   Objects
     1. COLUMNS   dbo.CompanySettings.IsSampleSegregationRequired          -> IsSentToDepartmentRequired
                  dbo.ServiceItemMaster.IsSampleSegregationRequired        -> IsSentToDepartmentRequired
                  dbo.PatientInvestigationDetails.IsSampleSegregated       -> IsSentToDepartment
                  dbo.PatientInvestigationDetails.SampleSegregatedBy       -> SentToDepartmentBy
                  dbo.PatientInvestigationDetails.SampleSegregationOn      -> SentToDepartmentOn
                  dbo.PatientInvestigationDetails.SampleSegregationDepartmentId -> SentToDepartmentId
     2. PROCEDURE dbo.S_searchPatientInvestigationForSentToDepartment (new name of
                  S_searchPatientInvestigationForSampleSegregation; old one dropped in part 3)
     3. PROCEDURES altered to use the new column names (text otherwise unchanged):
                  S_GetCompanySettings, S_GetServiceItemMaster, IU_ServiceItemMaster
                  (@isSampleSegregationRequired -> @isSentToDepartmentRequired),
                  S_GetPatientInvestigationDetails, S_GetAllInvestigationNameOfPatient,
                  S_searchPatientInvestigationForSampleManagement,
                  S_searchPatientInvestigationForSampleDepartmentReceiving,
                  S_SearchPatientInvestigationForSampleProcessingPathology, U_UpdateSampleStatus,
                  S_GetSampleJourneyByPatientInvestigationId, S_ValidatePatientInvestigationforRefund,
                  S_GetPatientInvestigationsForReportPrint,
                  S_GetLabReportPrintPreviewInvestigations
                  (output alias InvSampleSegregationDateTime -> InvSentToDepartmentDateTime)
     4. MENU ROW  dbo.NavigationSubMenuMaster 'Sample Segregation'/'sample-segregation'
                  -> 'Sent To Department'/'sent-to-department'

   NOT included: S_GetPatientInvestigationsForReportPrintDummy (alias InvSampleSegregationDateTime).
                 It already fails to compile (LabReportPrintSettingMaster has no NABLPath, DoctorSignature,
                 TestNameforSingleTest, PrintDepartmentSeperate) and the live API does not call it.

   After running: deploy the API and the frontend built from the same change
   (endpoint Lab/searchPatientInvestigationForSentToDepartment, route /sent-to-department).
   Clear the Redis menu cache / re-login so the new menu name and URL are loaded.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Columns ---------- */
IF COL_LENGTH('dbo.CompanySettings','IsSampleSegregationRequired') IS NOT NULL AND COL_LENGTH('dbo.CompanySettings','IsSentToDepartmentRequired') IS NULL
    EXEC sp_rename 'dbo.CompanySettings.IsSampleSegregationRequired', 'IsSentToDepartmentRequired', 'COLUMN';
IF COL_LENGTH('dbo.ServiceItemMaster','IsSampleSegregationRequired') IS NOT NULL AND COL_LENGTH('dbo.ServiceItemMaster','IsSentToDepartmentRequired') IS NULL
    EXEC sp_rename 'dbo.ServiceItemMaster.IsSampleSegregationRequired', 'IsSentToDepartmentRequired', 'COLUMN';
IF COL_LENGTH('dbo.PatientInvestigationDetails','IsSampleSegregated') IS NOT NULL AND COL_LENGTH('dbo.PatientInvestigationDetails','IsSentToDepartment') IS NULL
    EXEC sp_rename 'dbo.PatientInvestigationDetails.IsSampleSegregated', 'IsSentToDepartment', 'COLUMN';
IF COL_LENGTH('dbo.PatientInvestigationDetails','SampleSegregatedBy') IS NOT NULL AND COL_LENGTH('dbo.PatientInvestigationDetails','SentToDepartmentBy') IS NULL
    EXEC sp_rename 'dbo.PatientInvestigationDetails.SampleSegregatedBy', 'SentToDepartmentBy', 'COLUMN';
IF COL_LENGTH('dbo.PatientInvestigationDetails','SampleSegregationOn') IS NOT NULL AND COL_LENGTH('dbo.PatientInvestigationDetails','SentToDepartmentOn') IS NULL
    EXEC sp_rename 'dbo.PatientInvestigationDetails.SampleSegregationOn', 'SentToDepartmentOn', 'COLUMN';
IF COL_LENGTH('dbo.PatientInvestigationDetails','SampleSegregationDepartmentId') IS NOT NULL AND COL_LENGTH('dbo.PatientInvestigationDetails','SentToDepartmentId') IS NULL
    EXEC sp_rename 'dbo.PatientInvestigationDetails.SampleSegregationDepartmentId', 'SentToDepartmentId', 'COLUMN';
GO

/* ---------- 2 + 3. Procedures ---------- */
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[S_GetCompanySettings]
AS
BEGIN
    SET NOCOUNT ON;
    
        SELECT top 1
       IsSampleCollectionRequired
      ,IsSampleDepartmentReceivingRequired
      ,IsSentToDepartmentRequired
  FROM CompanySettings
 
          
END
GO

SET QUOTED_IDENTIFIER ON;
GO




CREATE OR ALTER PROCEDURE [dbo].[S_GetServiceItemMaster]
AS
BEGIN
    SELECT
        ServiceItemId, sim.HospId,cm.CategoryTypeId, sim.CategoryId, sim.SubCategoryId, sim.SubSubCategoryId,
        Name, Code, ReportTypeId, ReportType, IsSampleRequired, SampleTypeId,
        SampleTypeIdList, LabMethodId, ForGenderId, ForGender, IsOutSource,
        IsPrintAlone,IsSentToDepartmentRequired, IsDepartmentReceivingRequired, ShortName, SampleVolume,
        InvestigationComment, tatInMin
		,sim.IsActive
		,isnull(GSTPer,0.00) as GSTPer
		,RoomTypeId
        ,RoomType
        ,IsICU
		,cm.CategoryName
		,scm.SubCategoryName
		,sscm.SubSubCategoryName
		,isnull(scm.LabTypeId,0) as labTypeId
		,OPDConsultationTypeId
		,OPDConsultationType
		,SNOMEDCode
		,IsRequiredSeparatePerformingDoctor
		,DoctorDepartmentIds
		,isOnlineConsultationAllow
		,isTeleConsultationService
		,IsRegistrationCharge
		,RegistrationChargeValidityDays
		,PackageDurationDays
		,FORMAT(validityStartsFrom, 'dd-MM-yyyy') 'StartsFrom'
		,FORMAT(validityEndsOn, 'dd-MM-yyyy') 'ExpiresOn'
		,CASE
             WHEN cm.CategoryTypeId NOT IN (11, 12) THEN 0
             WHEN sim.ValidityStartsFrom IS NOT NULL
                  AND sim.ValidityEndsOn IS NOT NULL
                  AND CAST(GETDATE() AS DATE) BETWEEN CAST(sim.ValidityStartsFrom AS DATE)
                                                  AND CAST(sim.ValidityEndsOn AS DATE)
             THEN 0
             ELSE 1
         END AS IsPackageExpired,
		 (select top 1 SaltName from SaltNameMaster snm where snm.SaltNameId=sim.DescriptionId) as SaltName
	
    FROM ServiceItemMaster sim 
	inner join CategoryMaster cm on cm.CategoryId=sim.CategoryId
	inner join SubCategoryMaster scm on scm.SubCategoryId=sim.SubCategoryId
	inner join SubSubCategoryMaster sscm on sscm.SubSubCategoryId=sim.SubSubCategoryId
    ORDER BY Name
END
GO

SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[IU_ServiceItemMaster] (
	@hospId INT
	,@serviceItemId INT
	,@categoryId INT
	,@subCategoryId INT
	,@subSubCategoryId INT
	,@name NVARCHAR(256)
	,@code NVARCHAR(50) = NULL
	,@validityStartsFrom date=null    
    ,@validityEndsOn date=null  
	,@isMultipleVisitAllow INT = NULL
	,@visitDuration INT = NULL
	,@visitDurationType NVARCHAR(20) = NULL
	,@reportTypeId INT = NULL
	,@reportType NVARCHAR(100) = NULL
	,@isSampleRequired INT = NULL
	,@sampleTypeId INT = NULL
	,@sampleTypeList NVARCHAR(MAX) = NULL
	,@labMethodId INT = NULL
	,@forGenderId INT = NULL
	,@forGender NVARCHAR(100) = NULL
	,@isOutSource INT = 0
	,@isPrintAlone INT = NULL
	,@isSentToDepartmentRequired INT = NULL
	,@isDepartmentReceivingRequired INT = NULL
	,@isActive INT
	,@userId INT
	,@IpAddress NVARCHAR(20)
	,@ShortName NVARCHAR(250) = NULL
	,@SampleVolume NVARCHAR(50) = NULL
	,@InvestigationComment NVARCHAR(MAX) = NULL
	,@tatInMin INT = 0
	,@roomTypeId INT = NULL
	,@roomType NVARCHAR(50) = NULL
	,@isICU INT = NULL
	,@gstPer DECIMAL = 0
	,@OPDConsultationTypeId INT = NULL
	,@OPDConsultationType NVARCHAR(50) = NULL
	,@SNOMEDCode NVARCHAR(50) = NULL
	,@doctorDepartmentIds NVARCHAR(max) = NULL
	,@isRequiredSeparatePerformingDoctor INT = NULL
	,@isOnlineConsultationAllow INT = NULL
	,@isTeleConsultationService INT = NULL
	,@isRegistrationCharge INT = NULL
	,@registrationChargeValidityDays INT = NULL
	,@packageDurationDays INT = NULL
	,@Result INT = NULL OUTPUT
	)
AS
BEGIN
	-- Duplicate name check within same SubSubCategory
	IF EXISTS (
			SELECT 1
			FROM [dbo].[ServiceItemMaster]
			WHERE Name = @name
				AND SubSubCategoryId = @subSubCategoryId
				AND ServiceItemId <> @serviceItemId
			)
	BEGIN
		SET @Result = - 1;
	END
			-- Duplicate code check
	ELSE IF EXISTS (
			SELECT 1
			FROM [dbo].[ServiceItemMaster]
			WHERE Code = @code
				AND Code > ''
				AND ServiceItemId <> @serviceItemId
			)
	BEGIN
		SET @Result = - 1;
	END
	ELSE
	BEGIN
		IF (@serviceItemId = 0)
		BEGIN
			INSERT INTO ServiceItemMaster (
				HospId
				,CategoryId
				,SubCategoryId
				,SubSubCategoryId
				,Name
				,Code
				 ,ValidityStartsFrom      
				,ValidityEndsOn  
				,isMultipleVisitAllow
	            ,visitDuration
	            ,visitDurationType
				,ReportTypeId
				,ReportType
				,IsSampleRequired
				,SampleTypeId
				,SampleTypeIdList
				,LabMethodId
				,ForGenderId
				,ForGender
				,IsOutSource
				,IsPrintAlone
				,IsSentToDepartmentRequired
				,IsDepartmentReceivingRequired
				,ShortName
				,SampleVolume
				,InvestigationComment
				,tatInMin
				,IsActive
				,CreatedBy
				,IpAddress
				,RoomTypeId
				,RoomType
				,IsICU
				,GSTPer
				,OPDConsultationTypeId
				,OPDConsultationType
				,SNOMEDCode
				,IsRequiredSeparatePerformingDoctor
				,DoctorDepartmentIds
				,isOnlineConsultationAllow
				,isTeleConsultationService
				,IsRegistrationCharge
				,RegistrationChargeValidityDays
				,PackageDurationDays
				)
			VALUES (
				@hospId
				,@categoryId
				,@subCategoryId
				,@subSubCategoryId
				,@name
				,@code
				,@validityStartsFrom      
				,@validityEndsOn  
				,@isMultipleVisitAllow
	            ,@visitDuration
	            ,@visitDurationType
				,@reportTypeId
				,@reportType
				,@isSampleRequired
				,@sampleTypeId
				,@sampleTypeList
				,@labMethodId
				,@forGenderId
				,@forGender
				,@isOutSource
				,@isPrintAlone
				,@isSentToDepartmentRequired
				,@isDepartmentReceivingRequired
				,@ShortName
				,@SampleVolume
				,@InvestigationComment
				,@tatInMin
				,@isActive
				,@userId
				,@IpAddress
				,@roomTypeId
				,@roomType
				,@isICU
				,@gstPer
				,@OPDConsultationTypeId
				,@OPDConsultationType
				,@SNOMEDCode
				,@isRequiredSeparatePerformingDoctor
				,@doctorDepartmentIds
				,@isOnlineConsultationAllow
				,@isTeleConsultationService
				,@isRegistrationCharge
				,@registrationChargeValidityDays
				,@packageDurationDays
				)

			SET @Result = @@IDENTITY;
		END
		ELSE
		BEGIN
			UPDATE ServiceItemMaster
			SET HospId = @hospId
				,CategoryId = @categoryId
				,SubCategoryId = @subCategoryId
				,SubSubCategoryId = @subSubCategoryId
				,Name = @name
				,Code = @code
				,ValidityStartsFrom=@validityStartsFrom      
				,ValidityEndsOn=@validityEndsOn  
				,isMultipleVisitAllow=@isMultipleVisitAllow
	            ,visitDuration		   =@visitDuration
	            ,visitDurationType	   =@visitDurationType
				,ReportTypeId = @reportTypeId
				,ReportType = @reportType
				,IsSampleRequired = @isSampleRequired
				,SampleTypeId = @sampleTypeId
				,SampleTypeIdList = @sampleTypeList
				,LabMethodId = @labMethodId
				,ForGenderId = @forGenderId
				,ForGender = @forGender
				,IsOutSource = @isOutSource
				,IsPrintAlone = @isPrintAlone
				,IsSentToDepartmentRequired = @isSentToDepartmentRequired
				,IsDepartmentReceivingRequired = @isDepartmentReceivingRequired
				,ShortName = @ShortName
				,SampleVolume = @SampleVolume
				,InvestigationComment = @InvestigationComment
				,tatInMin = @tatInMin
				,IsActive = @isActive
				,LastModifiedBy = @userId
				,LastModifiedOn = GETDATE()
				,IpAddress = @IpAddress
				,RoomTypeId = @roomTypeId
				,RoomType = @roomType
				,IsICU = @isICU
				,GSTPer = @gstPer
				,OPDConsultationTypeId = @OPDConsultationTypeId
				,OPDConsultationType = @OPDConsultationType
				,SNOMEDCode = @SNOMEDCode
				,IsRequiredSeparatePerformingDoctor=@isRequiredSeparatePerformingDoctor
				,DoctorDepartmentIds=@doctorDepartmentIds
				,isOnlineConsultationAllow = @isOnlineConsultationAllow
				,isTeleConsultationService = @isTeleConsultationService
				,IsRegistrationCharge=@isRegistrationCharge
				,RegistrationChargeValidityDays=@registrationChargeValidityDays
				,PackageDurationDays=@packageDurationDays
			WHERE ServiceItemId = @serviceItemId

			SET @Result = @serviceItemId;
		END
	END

	SELECT @Result;
END
GO

SET QUOTED_IDENTIFIER ON;
GO


CREATE OR ALTER PROCEDURE [dbo].[S_GetPatientInvestigationDetails]              
@branchId int,                
@uhid NVARCHAR(100),                      
@labNo int,                    
@visitId int                    
            
AS                      
BEGIN                      
                      
 SELECT                       
                       
   PM.UHID, 
   PVD.VisitNo,
   PID.LabNo,                      
   PM.PatientName,    
   PVD.CurrentAge,    
   PM.Gender,    
   PID.LabNo,    
   PID.BarCode,  
   sm.LabTypeId,
   sm.LabType,
   sm.SubCategoryName,
   SIM.Name,          
   FORMAT(PID.CreatedOn,'dd-MMM-yyyy')BillDate,
   isnull((select top 1 name from usermaster where id=pid.CreatedBy),'')BillingBy,
   isnull((FORMAT(PID.SampleCollectedOn,'dd-MMM-yyyy')),'')SampleCollectedOn,
   isnull((select top 1 name from usermaster where id=pid.SampleCollectedBy),'')SampleCollectedBy,
   isnull((FORMAT(PID.SampleReceivedByDepartmentOn,'dd-MMM-yyyy')),'')SampleReceivedByDepartmentOn,
   isnull((select top 1 name from usermaster where id=pid.SampleReceivedBy),'')SampleReceivedBy,
   isnull((FORMAT(PID.SampleCollectionRejectedOn,'dd-MMM-yyyy')),'')SampleCollectionRejectedOn,
   isnull((select top 1 name from usermaster where id=pid.SampleCollectionRejectedBy),'')SampleCollectionRejectedBy,
   isnull((pid.SampleCollectionRejectionReason),'')SampleCollectionRejectionReason,
   isnull((FORMAT(PID.ResultDoneOn,'dd-MMM-yyyy')),'')ResultDoneOn,
   isnull((select top 1 name from usermaster where id=pid.ResultDoneBy),'')ResultDoneBy,
   isnull((FORMAT(PID.ReportApprovedOn,'dd-MMM-yyyy')),'')ApprovedDate,
   isnull((select top 1 name from usermaster where id=pid.ReportApprovedBy),'')ReportApprovedBy,
   PID.PatientInvestigationId ,
   PID.InvestigationId ,
   SIM.ReportTypeId , 
   PID.IsSentToDepartment,
   SIM.IsSampleRequired,                      
   SIM.IsSentToDepartmentRequired,                      
   SIM.IsDepartmentReceivingRequired,                      
   PID.IsSampleCollected,               
   PID.IsSampleReceivedByDepartment,              
   PID.IsResultDone,              
   PID.IsReportApproved,              
   PID.IsDispatched  ,
   pid.isUrgent,
   isnull(pid.NotificationRemark,'')NotificationRemark
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0 AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId=@branchId                     
AND PID.LabNo=@labNo    
AND PID.VisitId=@visitId 
INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0                 
 and PVD.UHID = @uhid    
 
INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId=@branchId           

 WHERE PID.IsResultDone in (0,1)             
                   
             
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetAllInvestigationNameOfPatient]              
@branchId int,                
@uhid NVARCHAR(100)=NULL,                      
@labTypeId int,                    
@labNo int,                    
@visitId int                    
            
AS                      
BEGIN                      
                      
 SELECT                       
                       
   PM.UHID, 
   PVD.VisitNo,
   PID.LabNo,                      
   PM.PatientName,    
   PVD.CurrentAge,  
      PM.ContactNumber,    
   PM.Gender,    
   PID.BarCode, 
   (select top 1 dm.CompleteName from DoctorMaster dm where dm.DoctorId=pid.DoctorId) as DoctorName,
   (select top 1 dm.Department from DoctorMaster dm where dm.DoctorId=pid.DoctorId) as DoctorDepartment,
   (select top 1 STM.SampleType from SampleTypeMaster STM where STM.SampleTypeId=pid.SampleTypeId) as SampleType,
    ISNULL(SIM.TATInMin, 0) AS TargetTATInMin,

        CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin,
      case when pvd.TypeId=2 then pvd.VisitNo else '' end as IPDNo,
      case when pvd.TypeId=2 then FORMAT(PVD.AdmissionDate,'dd-MMM-yyyy') else '' end as AdmissionDate,
      case when pvd.TypeId=2 then CONVERT(varchar(15), PVD.AdmissionTime, 100) else '' end as AdmissionTime,
  isnull((SELECT CONCAT(WNM.WardName,'/',BM.RoomName,'/',BM.BedNo) FROM BedMaster BM inner join wardNameMaster WNM on WNM.WardNameId=BM.WardNameId WHERE BM.BedId=PVD.BedId),'') BedNo,                                        

   SIM.Name,          
   FORMAT(PID.CreatedOn,'dd-MMM-yyyy hh:mm tt')BillDate,
   isnull((select top 1 name from usermaster where id=pid.CreatedBy),'')BillingBy,
   isnull((FORMAT(PID.SampleCollectedOn,'dd-MMM-yyyy hh:mm tt')),'')SampleCollectedOn,
   isnull((select top 1 name from usermaster where id=pid.SampleCollectedBy),'')SampleCollectedBy,
   isnull((FORMAT(PID.SentToDepartmentOn,'dd-MMM-yyyy hh:mm tt')),'')SentToDepartmentOn,
   isnull((select top 1 name from usermaster where id=pid.SentToDepartmentBy),'')SentToDepartmentBy,
   isnull((FORMAT(PID.SampleReceivedByDepartmentOn,'dd-MMM-yyyy hh:mm tt')),'')SampleReceivedByDepartmentOn,
   isnull((select top 1 name from usermaster where id=pid.SampleReceivedBy),'')SampleReceivedBy,
   
   isnull((FORMAT(PID.ResultDoneOn,'dd-MMM-yyyy hh:mm tt')),'')ResultDoneOn,
   isnull((select top 1 name from usermaster where id=pid.ResultDoneBy),'')ResultDoneBy,
   isnull((FORMAT(PID.ReportApprovedOn,'dd-MMM-yyyy hh:mm tt')),'')ApprovedDate,
   isnull((select top 1 name from usermaster where id=pid.ReportApprovedBy),'')ReportApprovedBy,
   isnull((FORMAT(PID.DispatchedOn,'dd-MMM-yyyy hh:mm tt')),'')DispatchedOn,
   isnull((select top 1 name from usermaster where id=pid.DispatchedBy),'')DispatchedBy,
   isnull((FORMAT(PID.ReportPrintedOn,'dd-MMM-yyyy hh:mm tt')),'')ReportPrintedOn,
   isnull((select top 1 name from usermaster where id=pid.ReportPrintedBy),'')ReportPrintedBy,

   

   PID.PatientInvestigationId ,
   PID.InvestigationId ,
   SIM.ReportTypeId ,
   PID.IsSampleCollected,               
   SIM.IsSampleRequired, 
   PID.IsSentToDepartment,
   SIM.IsSentToDepartmentRequired,  
   PID.IsSampleReceivedByDepartment,              
   SIM.IsDepartmentReceivingRequired,                      
   PID.IsResultDone,              
   PID.IsReportApproved,              
   PID.IsDispatched  ,
   PID.isReportPrinted  ,
   pid.isUrgent,
   pid.IsReportHold,
   pid.IsAbnormalResult,
   isnull(pid.NotificationRemark,0)NotificationRemark,
   '' as MachineName,
    '' as Connection,
    '' as QcStatus,
    '' as LastSync,
	(select top 1 ContentValue from InvestigationTemplateInterpretationMapping i 
	inner join InvastigationTemplateCommentMaster it on it.id=i.ItemId
where it.TypeId=2 and i.InvestigationId=167) as InterpretationValue

 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0 AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 --AND PID.BranchId=@branchId                     
AND PID.LabNo=@labNo 
AND PID.VisitId=@visitId 
INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0                 
 and PVD.UHID = @uhid    
 
INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId and sim.ReportTypeId in (1,2)
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and sm.LabTypeId=@labTypeId
-- INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId=@branchId           

 WHERE PID.IsResultDone in (0,1) 
 and  pid.barcode!='SNR' and pid.isSampleRejected=0 
 and (pid.IsSampleCollected=1 or sim.IsSampleRequired=0 or  (select top 1 IsSampleCollectionRequired from CompanySettings) = 0)
 and (pid.IsSentToDepartment=1 or sim.IsSentToDepartmentRequired=0 or (select top 1 IsSentToDepartmentRequired from CompanySettings) = 0)
 and (pid.IsSampleReceivedByDepartment=1 or sim.IsDepartmentReceivingRequired=0 or (select top 1 IsSampleDepartmentReceivingRequired from CompanySettings) = 0)       
; 
             
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO






CREATE OR ALTER PROCEDURE [dbo].[S_searchPatientInvestigationForSampleManagement]              
@branchId NVARCHAR(10),                
@typeId NVARCHAR(10),               
@uhid NVARCHAR(100)=NULL,                      
@ipdNo NVARCHAR(10)=NULL,                      
@labNo NVARCHAR(10)=NULL,                      
@fromDate NVARCHAR(20)=NULL,                      
@toDate NVARCHAR(20)=NULL,                      
@barCode NVARCHAR(20)=NULL,            
@subCategoryId NVARCHAR(20)=NULL,            
@subSubCategoryId NVARCHAR(20)=NULL,             
@corporateId NVARCHAR(20)=NULL,             
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',
@statusId NVARCHAR(20)='0',
@sampleTypeId NVARCHAR(20)='0',
@roomTypeId NVARCHAR(20)='0',
@isUrgent NVARCHAR(20)=NULL
            
AS                      
BEGIN                      
 DECLARE @SQL NVARCHAR(MAX)=NULL;                      
                      
 SET @SQL='                      
 SELECT                       
   FORMAT(PID.CreatedOn,''dd-MMM-yyyy'') AS BillDate,                      
   PVD.Type,                       
   PM.UHID,                      
   CASE WHEN PVD.TypeId=2 THEN PVD.VisitNo ELSE '''' END AS IPDNo,                      
   PID.LabNo,                      
   '''' AS WardName,                      
   PM.PatientName,                      
   PVD.CurrentAge ,          
   (select top 1 concat(CorporateCode,'' '',CorporateName) from CorporateMaster where CorporateId=pvd.CorporateId)CorporateName,
   PM.Gender,                      
   PVD.TotalBalanceAmount,
   PID.VisitId,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired,       
   sim.IsSentToDepartmentRequired,
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
   ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
   PID.IsSampleCollected,               
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,  
   pid.IsSentToDepartment,
   ISNULL(CONVERT(varchar(100),PID.SentToDepartmentOn,100) , '''') SentToDepartmentOn, 
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected,
   pid.IsAbnormalResult,
   pid.isReportPrinted,
   ISNULL(CONVERT(varchar(100),PID.ReportPrintedOn,100) , '''') ReportPrintedOn  ,


   case when pid.SampleTypeId!=0 then pid.SampleTypeId else sim.SampleTypeId end as DefaultSampleTypeId,

ISNULL(STUFF((SELECT ''$'' + CAST(STM.SampleTypeId AS NVARCHAR) + ''*'' + STM.SampleType
    + ''*'' + ISNULL((SELECT TOP 1 ColorCode FROM SampleContainerColorMaster WHERE STM.ContainerColorId = ColorId), '''') ColorCode
    FROM SampleTypeMaster STM
    WHERE STM.SampleTypeId IN (
        SELECT TRY_CAST(value AS INT)
        FROM STRING_SPLIT(SIM.SampleTypeIdList, '','')
        WHERE TRY_CAST(value AS INT) IS NOT NULL
    )
    FOR XML PATH(''''), TYPE).value(''.'', ''NVARCHAR(MAX)''), 1, 1, ''''), '''') SampleTypeList,


   case when TatInHour!=0 and TatInHour is not null and PID.SampleCollectedOn is not null and pid.isSampleRejected=0 then ISNULL(CONVERT(varchar(100),dateadd(hour,TatInHour,PID.SampleCollectedOn),100) , '''') else ''''  end as DeliveryDate
  ,ftd.IsUnderPackage
  ,case when ftd.IsUnderPackage=1 then concat(isnull((select top 1 SIM2.Name from ServiceItemMaster SIM2 where SIM2.ServiceItemId=ftd.PackageId),''''),'' : '',SIM.Name) else SIM.Name end as Name
  ,(SELECT CONCAT(WNM.WardName,''/'',BM.RoomName,''/'',BM.BedNo) FROM BedMaster BM inner join wardNameMaster WNM on WNM.WardNameId=BM.WardNameId WHERE BM.BedId=PVD.BedId) BedNo                                        
        ,ISNULL(SIM.TATInMin, 0) AS TargetTATInMin

        ,CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin
 
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0   AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
 IF(@labNo IS NOT NULL AND LEN(@labNo)>0)                      
  SET @SQL+=' AND PID.LabNo='+@labNo+''                      
                       
 IF(@barCode IS NOT NULL AND LEN(@barCode)>0)                      
  SET @SQL+=' AND PID.Barcode='''+@barCode+''''                      
                      
 SET @SQL+=' INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0 '                      
                      
 IF(@typeId<>'0' AND LEN(@typeId)>0)                      
  SET @SQL+=' AND PVD.TypeId='+@typeId+''                      
                       
 IF(@uhid IS NOT NULL AND LEN(@uhid)>0)                      
  SET @SQL+=' AND PVD.UHID like ''%'+@uhid+'%'''                      
                      
 IF(@ipdNo IS NOT NULL AND LEN(@ipdNo)>0)                      
  SET @SQL+=' AND TypeId=2 AND PVD.VisitNo='''+@ipdNo+''''                      
               
 IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
  

 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and SM.LabTypeId=1
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
            
 IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' INNER JOIN SubSubCategoryMaster SSCM on SSCM.SubCategoryId=SIM.SubCategoryId'     
  
      IF(@roomTypeId <>'0' AND LEN(@roomTypeId)>0)                      
  SET @SQL+=' INNER JOIN BedMaster bm on bm.bedId=pvd.bedId and bm.TypeId = '+@roomTypeId+''    
       
 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '   
 
   IF(@sampleTypeId <>'0' AND LEN(@sampleTypeId)>0)                      
  SET @SQL+=' AND isnull(pid.sampleTypeId,sim.sampleTypeId) = '+@sampleTypeId+'' 

 IF(@isUrgent IS NOT NULL AND LEN(@isUrgent)>0)                      
  SET @SQL+=' AND pid.isUrgent = '+@isUrgent+''  
            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND SSCM.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''             
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''   
  

            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '                      
             
			 --sample management collection  
 SET @SQL+=' and (select top 1 IsSampleCollectionRequired from CompanySettings) = 1 '                  
 SET @SQL+=' and sim.IsSampleRequired=1'                                  
                 
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO








CREATE OR ALTER PROCEDURE [dbo].[S_searchPatientInvestigationForSentToDepartment]              
@branchId NVARCHAR(10),                
@typeId NVARCHAR(10),               
@uhid NVARCHAR(100)=NULL,                      
@ipdNo NVARCHAR(10)=NULL,                      
@labNo NVARCHAR(10)=NULL,                      
@fromDate NVARCHAR(20)=NULL,                      
@toDate NVARCHAR(20)=NULL,                      
@barCode NVARCHAR(20)=NULL,            
@subCategoryId NVARCHAR(20)=NULL,            
@subSubCategoryId NVARCHAR(20)=NULL,             
@corporateId NVARCHAR(20)=NULL,             
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',
@statusId NVARCHAR(20)='0',
@sampleTypeId NVARCHAR(20)='0',
@roomTypeId NVARCHAR(20)='0',
@isUrgent NVARCHAR(20)=NULL
            
AS                      
BEGIN                      
 DECLARE @SQL NVARCHAR(MAX)=NULL;                      

 SET @SQL='                      
 SELECT                       
   FORMAT(PID.CreatedOn,''dd-MMM-yyyy'') AS BillDate,                      
   PVD.Type,  
   PM.UHID,                      
   CASE WHEN PVD.TypeId=2 THEN PVD.VisitNo ELSE '''' END AS IPDNo,                      
   PID.LabNo,                      
   '''' AS WardName,                      
   PM.PatientName,                      
   PVD.CurrentAge ,          
   (select top 1 concat(CorporateCode,'' '',CorporateName) from CorporateMaster where CorporateId=pvd.CorporateId)CorporateName,
   PM.Gender,                      
   PID.VisitId,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired,       
   sim.IsSentToDepartmentRequired,
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
   ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
   PID.IsSampleCollected,               
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,          
   pid.IsSentToDepartment,
   ISNULL(CONVERT(varchar(100),PID.SentToDepartmentOn,100) , '''') SentToDepartmentOn,
   PID.SentToDepartmentId,
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected,
   pid.IsAbnormalResult,
   pid.isReportPrinted,
   ISNULL(CONVERT(varchar(100),PID.ReportPrintedOn,100) , '''') ReportPrintedOn  ,

   case when pid.SampleTypeId!=0 then pid.SampleTypeId else sim.SampleTypeId end as DefaultSampleTypeId,

ISNULL(STUFF((SELECT ''$'' + CAST(STM.SampleTypeId AS NVARCHAR) + ''*'' + STM.SampleType
    + ''*'' + ISNULL((SELECT TOP 1 ColorCode FROM SampleContainerColorMaster WHERE STM.ContainerColorId = ColorId), '''') ColorCode
    FROM SampleTypeMaster STM
    WHERE STM.SampleTypeId IN (
        SELECT TRY_CAST(value AS INT)
        FROM STRING_SPLIT(SIM.SampleTypeIdList, '','')
        WHERE TRY_CAST(value AS INT) IS NOT NULL
    )
    FOR XML PATH(''''), TYPE).value(''.'', ''NVARCHAR(MAX)''), 1, 1, ''''), '''') SampleTypeList,


   case when TatInHour!=0 and TatInHour is not null and PID.SampleCollectedOn is not null and pid.isSampleRejected=0 then ISNULL(CONVERT(varchar(100),dateadd(hour,TatInHour,PID.SampleCollectedOn),100) , '''') else ''''  end as DeliveryDate
  ,ftd.IsUnderPackage
  ,case when ftd.IsUnderPackage=1 then concat(isnull((select top 1 SIM2.Name from ServiceItemMaster SIM2 where SIM2.ServiceItemId=ftd.PackageId),''''),'' : '',SIM.Name) else SIM.Name end as Name
  ,(SELECT CONCAT(WNM.WardName,''/'',BM.RoomName,''/'',BM.BedNo) FROM BedMaster BM inner join wardNameMaster WNM on WNM.WardNameId=BM.WardNameId WHERE BM.BedId=PVD.BedId) BedNo                                        
        ,ISNULL(SIM.TATInMin, 0) AS TargetTATInMin

        ,CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin
 
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0   AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
 IF(@labNo IS NOT NULL AND LEN(@labNo)>0)                      
  SET @SQL+=' AND PID.LabNo='+@labNo+''                      
                       
 IF(@barCode IS NOT NULL AND LEN(@barCode)>0)                      
  SET @SQL+=' AND PID.Barcode='''+@barCode+''''                      
                      
 SET @SQL+=' INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0 '                      
                      
 IF(@typeId<>'0' AND LEN(@typeId)>0)                      
  SET @SQL+=' AND PVD.TypeId='+@typeId+''                      
                       
 IF(@uhid IS NOT NULL AND LEN(@uhid)>0)                      
  SET @SQL+=' AND PVD.UHID like ''%'+@uhid+'%'''                      
                      
 IF(@ipdNo IS NOT NULL AND LEN(@ipdNo)>0)                      
  SET @SQL+=' AND TypeId=2 AND PVD.VisitNo='''+@ipdNo+''''                      
               
 IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
  

 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and SM.LabTypeId=1
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
            
 IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' INNER JOIN SubSubCategoryMaster SSCM on SSCM.SubCategoryId=SIM.SubCategoryId'     
  
      IF(@roomTypeId <>'0' AND LEN(@roomTypeId)>0)                      
  SET @SQL+=' INNER JOIN BedMaster bm on bm.bedId=pvd.bedId and bm.TypeId = '+@roomTypeId+''    
       
 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '   
 
   IF(@sampleTypeId <>'0' AND LEN(@sampleTypeId)>0)                      
  SET @SQL+=' AND isnull(pid.sampleTypeId,sim.sampleTypeId) = '+@sampleTypeId+'' 

 IF(@isUrgent IS NOT NULL AND LEN(@isUrgent)>0)                      
  SET @SQL+=' AND pid.isUrgent = '+@isUrgent+''  
            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND SSCM.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''             
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''   
  

            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '                      
         
		 --Sent To Department           
 SET @SQL+=' and (select top 1 IsSentToDepartmentRequired from CompanySettings) = 1 '                  
 SET @SQL+=' and sim.IsSentToDepartmentRequired=1'                                  
 SET @SQL+=' and (pid.IsSampleCollected=1 or sim.IsSampleRequired=0 or (select top 1 IsSampleCollectionRequired from CompanySettings) = 0)'                                  
                 
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO






CREATE OR ALTER PROCEDURE [dbo].[S_searchPatientInvestigationForSampleDepartmentReceiving]              
@branchId NVARCHAR(10),                
@typeId NVARCHAR(10),               
@uhid NVARCHAR(100)=NULL,                      
@ipdNo NVARCHAR(10)=NULL,                      
@labNo NVARCHAR(10)=NULL,                      
@fromDate NVARCHAR(20)=NULL,                      
@toDate NVARCHAR(20)=NULL,                      
@barCode NVARCHAR(20)=NULL,            
@subCategoryId NVARCHAR(20)=NULL,            
@subSubCategoryId NVARCHAR(20)=NULL,             
@corporateId NVARCHAR(20)=NULL,             
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',
@statusId NVARCHAR(20)='0',
@sampleTypeId NVARCHAR(20)='0',
@roomTypeId NVARCHAR(20)='0',
@isUrgent NVARCHAR(20)=NULL
            
AS                      
BEGIN                      
 DECLARE @SQL NVARCHAR(MAX)=NULL;                      
                      
 SET @SQL='                      
 SELECT                       
   FORMAT(PID.CreatedOn,''dd-MMM-yyyy'') AS BillDate,                      
   PVD.Type,                       
   PM.UHID,                      
   CASE WHEN PVD.TypeId=2 THEN PVD.VisitNo ELSE '''' END AS IPDNo,                      
   PID.LabNo,                      
   '''' AS WardName,                      
   PM.PatientName,                      
   PVD.CurrentAge ,          
   (select top 1 concat(CorporateCode,'' '',CorporateName) from CorporateMaster where CorporateId=pvd.CorporateId)CorporateName,
   PM.Gender,                      
   PVD.TotalBalanceAmount,
   PID.VisitId,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired, 
   sim.IsSentToDepartmentRequired,
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
   ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
   PID.IsSampleCollected,               
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,     
   pid.IsSentToDepartment,
   ISNULL(CONVERT(varchar(100),PID.SentToDepartmentOn,100) , '''') SentToDepartmentOn,     
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected,
   pid.IsAbnormalResult,
   pid.isReportPrinted,
   ISNULL(CONVERT(varchar(100),PID.ReportPrintedOn,100) , '''') ReportPrintedOn  ,


   case when pid.SampleTypeId!=0 then pid.SampleTypeId else sim.SampleTypeId end as DefaultSampleTypeId,

ISNULL(STUFF((SELECT ''$'' + CAST(STM.SampleTypeId AS NVARCHAR) + ''*'' + STM.SampleType
    + ''*'' + ISNULL((SELECT TOP 1 ColorCode FROM SampleContainerColorMaster WHERE STM.ContainerColorId = ColorId), '''') ColorCode
    FROM SampleTypeMaster STM
    WHERE STM.SampleTypeId IN (
        SELECT TRY_CAST(value AS INT)
        FROM STRING_SPLIT(SIM.SampleTypeIdList, '','')
        WHERE TRY_CAST(value AS INT) IS NOT NULL
    )
    FOR XML PATH(''''), TYPE).value(''.'', ''NVARCHAR(MAX)''), 1, 1, ''''), '''') SampleTypeList,


   case when TatInHour!=0 and TatInHour is not null and PID.SampleCollectedOn is not null and pid.isSampleRejected=0 then ISNULL(CONVERT(varchar(100),dateadd(hour,TatInHour,PID.SampleCollectedOn),100) , '''') else ''''  end as DeliveryDate
  ,ftd.IsUnderPackage
  ,case when ftd.IsUnderPackage=1 then concat(isnull((select top 1 SIM2.Name from ServiceItemMaster SIM2 where SIM2.ServiceItemId=ftd.PackageId),''''),'' : '',SIM.Name) else SIM.Name end as Name
  ,(SELECT CONCAT(WNM.WardName,''/'',BM.RoomName,''/'',BM.BedNo) FROM BedMaster BM inner join wardNameMaster WNM on WNM.WardNameId=BM.WardNameId WHERE BM.BedId=PVD.BedId) BedNo                                        
        ,ISNULL(SIM.TATInMin, 0) AS TargetTATInMin

        ,CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin
 
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0   AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
 IF(@labNo IS NOT NULL AND LEN(@labNo)>0)                      
  SET @SQL+=' AND PID.LabNo='+@labNo+''                      
                       
 IF(@barCode IS NOT NULL AND LEN(@barCode)>0)                      
  SET @SQL+=' AND PID.Barcode='''+@barCode+''''                      
                      
 SET @SQL+=' INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0 '                      
                      
 IF(@typeId<>'0' AND LEN(@typeId)>0)                      
  SET @SQL+=' AND PVD.TypeId='+@typeId+''                      
                       
 IF(@uhid IS NOT NULL AND LEN(@uhid)>0)                      
  SET @SQL+=' AND PVD.UHID like ''%'+@uhid+'%'''                      
                      
 IF(@ipdNo IS NOT NULL AND LEN(@ipdNo)>0)                      
  SET @SQL+=' AND TypeId=2 AND PVD.VisitNo='''+@ipdNo+''''                      
               
 IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
  

 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and SM.LabTypeId=1
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
            
 IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' INNER JOIN SubSubCategoryMaster SSCM on SSCM.SubCategoryId=SIM.SubCategoryId'     
  
      IF(@roomTypeId <>'0' AND LEN(@roomTypeId)>0)                      
  SET @SQL+=' INNER JOIN BedMaster bm on bm.bedId=pvd.bedId and bm.TypeId = '+@roomTypeId+''    
       
 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '   
 
   IF(@sampleTypeId <>'0' AND LEN(@sampleTypeId)>0)                      
  SET @SQL+=' AND isnull(pid.sampleTypeId,sim.sampleTypeId) = '+@sampleTypeId+'' 

 IF(@isUrgent IS NOT NULL AND LEN(@isUrgent)>0)                      
  SET @SQL+=' AND pid.isUrgent = '+@isUrgent+''  
            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND SSCM.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''             
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''   
  

            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '
  
       --department receiving            
 SET @SQL+=' and (select top 1 IsSampleDepartmentReceivingRequired from CompanySettings) = 1 '                  
 SET @SQL+=' and sim.IsDepartmentReceivingRequired=1'                                  
 SET @SQL+=' and (pid.IsSampleCollected=1 or sim.IsSampleRequired=0 or  (select top 1 IsSampleCollectionRequired from CompanySettings) = 0)'                                  
 SET @SQL+=' and (pid.IsSentToDepartment=1 or sim.IsSentToDepartmentRequired=0 or (select top 1 IsSentToDepartmentRequired from CompanySettings) = 0)'      
 
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO


CREATE OR ALTER PROCEDURE [dbo].[S_SearchPatientInvestigationForSampleProcessingPathology]              
@branchId NVARCHAR(10),                
@typeId NVARCHAR(10),               
@uhid NVARCHAR(100)=NULL,                      
@ipdNo NVARCHAR(10)=NULL,                      
@labNo NVARCHAR(10)=NULL,                      
@fromDate NVARCHAR(20)=NULL,                      
@toDate NVARCHAR(20)=NULL,                      
@barCode NVARCHAR(20)=NULL,            
@subCategoryId NVARCHAR(20)=NULL,            
@subSubCategoryId NVARCHAR(20)=NULL,             
@corporateId NVARCHAR(20)=NULL,             
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',
@statusId NVARCHAR(20)='0',
@canSampleCollect NVARCHAR(20)='0'
            
AS                      
BEGIN                      
 DECLARE @SQL NVARCHAR(MAX)=NULL;                      
                      
 SET @SQL='                      
 SELECT                       
   FORMAT(PID.CreatedOn,''dd-MMM-yyyy'') AS BillDate,                      
   PVD.Type,                       
   PM.UHID,                      
   CASE WHEN PVD.TypeId=2 THEN PVD.VisitNo ELSE '''' END AS IPDNo,                      
   PID.LabNo,                      
   PM.PatientName,                      
   PVD.CurrentAge ,          
   (select top 1 concat(CorporateCode,'' '',CorporateName) from CorporateMaster where CorporateId=pvd.CorporateId)CorporateName,
   PM.Gender,                      
   PVD.TotalBalanceAmount,

   PID.VisitId,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired,           
   sim.IsSentToDepartmentRequired,
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
   ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
   PID.IsSampleCollected,               
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,   
   pid.IsSentToDepartment,
   ISNULL(CONVERT(varchar(100),PID.SentToDepartmentOn,100) , '''') SentToDepartmentOn, 
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected,
   pid.IsAbnormalResult,
   pid.isReportPrinted,
   ISNULL(CONVERT(varchar(100),PID.ReportPrintedOn,100) , '''') ReportPrintedOn,

   case when pid.SampleTypeId!=0 then pid.SampleTypeId else sim.SampleTypeId end as DefaultSampleTypeId,

   ISNULL(STUFF((SELECT ''$'' + CAST(STM.SampleTypeId AS NVARCHAR) + ''*'' + STM.SampleType
    + ''*'' + ISNULL((SELECT TOP 1 ColorCode FROM SampleContainerColorMaster WHERE STM.ContainerColorId = ColorId), '''') ColorCode
    FROM SampleTypeMaster STM
    WHERE STM.SampleTypeId IN (
        SELECT TRY_CAST(value AS INT)
        FROM STRING_SPLIT(SIM.SampleTypeIdList, '','')
        WHERE TRY_CAST(value AS INT) IS NOT NULL
    )
    FOR XML PATH(''''), TYPE).value(''.'', ''NVARCHAR(MAX)''), 1, 1, ''''), '''') SampleTypeList,
  


   case when TatInHour!=0 and TatInHour is not null and PID.SampleCollectedOn is not null and pid.isSampleRejected=0 then ISNULL(CONVERT(varchar(100),dateadd(hour,TatInHour,PID.SampleCollectedOn),100) , '''') else ''''  end as DeliveryDate
  ,ftd.IsUnderPackage
  ,case when ftd.IsUnderPackage=1 then concat(isnull((select top 1 SIM2.Name from ServiceItemMaster SIM2 where SIM2.ServiceItemId=ftd.PackageId),''''),'' : '',SIM.Name) else SIM.Name end as Name
  ,(SELECT CONCAT(WNM.WardName,''/'',BM.RoomName,''/'',BM.BedNo) FROM BedMaster BM inner join wardNameMaster WNM on WNM.WardNameId=BM.WardNameId WHERE BM.BedId=PVD.BedId) BedNo                                        
        ,ISNULL(SIM.TATInMin, 0) AS TargetTATInMin

        ,CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin
 
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0   AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
 IF(@labNo IS NOT NULL AND LEN(@labNo)>0)                      
  SET @SQL+=' AND PID.LabNo='+@labNo+'' 

                       
 IF(@barCode IS NOT NULL AND LEN(@barCode)>0)                      
  SET @SQL+=' AND PID.Barcode='''+@barCode+''''                      
                      
 SET @SQL+=' INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId AND PVD.IsCancel=0 '                      
                      
 IF(@typeId<>'0' AND LEN(@typeId)>0)                      
  SET @SQL+=' AND PVD.TypeId='+@typeId+''                      
                       
 IF(@uhid IS NOT NULL AND LEN(@uhid)>0)                      
  SET @SQL+=' AND PVD.UHID like ''%'+@uhid+'%'''                      
                      
 IF(@ipdNo IS NOT NULL AND LEN(@ipdNo)>0)                      
  SET @SQL+=' AND TypeId=2 AND PVD.VisitNo='''+@ipdNo+''''                      
               
 IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
  

 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and SM.LabTypeId=1
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
            
 IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' INNER JOIN SubSubCategoryMaster SSCM on SSCM.SubCategoryId=SIM.SubCategoryId' 
  
    
          
 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '              
            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND SSCM.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''             
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''              
            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL) and (@uhid IS NULL or LEN(@uhid)=0))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL)  and (@uhid IS NULL or LEN(@uhid)=0))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '                      
                      
                 

--IF (ISNULL(LTRIM(RTRIM(@canSampleCollect)), '') = '0')
 SET @SQL+=' and (pid.IsSampleCollected=1 or sim.IsSampleRequired=0 or  (select top 1 IsSampleCollectionRequired from CompanySettings) = 0)'                                  
 SET @SQL+=' and (pid.IsSentToDepartment=1 or sim.IsSentToDepartmentRequired=0 or (select top 1 IsSentToDepartmentRequired from CompanySettings) = 0)'  
 SET @SQL+=' and (pid.IsSampleReceivedByDepartment=1 or sim.IsDepartmentReceivingRequired=0 or (select top 1 IsSampleDepartmentReceivingRequired from CompanySettings) = 0)'  
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO

SET QUOTED_IDENTIFIER ON;
GO


CREATE OR ALTER PROCEDURE [dbo].[U_UpdateSampleStatus]
@patientInvestigationId INT,
@barCode NVARCHAR(256),
@statusId INT,
@userId INT,
@sampleDateTime datetime=null,
@defaultSampleTypeId INT=0,
@departmentId INT=0,
@ipAddress NVARCHAR(20)=NULL
AS 
BEGIN

	IF(@statusId=1) ---Sample Collection
		BEGIN
			UPDATE
				PatientInvestigationDetails
			SET
				IsSampleCollected=1,
				isSampleRejected=0,
				SampleCollectedOn=isnull(@sampleDateTime,GETDATE()),
				SampleCollectedBy=@userId,
				BarCode=@barCode,
				SampleTypeId=@defaultSampleTypeId,
				LastModifiedBy=@userId,
				LastModifiedOn=GETDATE(),
				IpAddress=@ipAddress
			WHERE
				PatientInvestigationId=@patientInvestigationId
		END

		IF(@statusId=2) ---Sent To Department
		BEGIN
			UPDATE
				PatientInvestigationDetails
			SET
				IsSentToDepartment=1,
				SentToDepartmentOn=GETDATE(),
				SentToDepartmentBy=@userId,
				BarCode=@barCode,
				SentToDepartmentId=@departmentId,
				LastModifiedBy=@userId,
				LastModifiedOn=GETDATE(),
				IpAddress=@ipAddress
			WHERE
				PatientInvestigationId=@patientInvestigationId

		END

	IF(@statusId=3) ---Dept Receiving
		BEGIN
			UPDATE
				PatientInvestigationDetails
			SET
				IsSampleReceivedByDepartment=1,
				SampleReceivedByDepartmentOn=isnull(@sampleDateTime,GETDATE()),
				SampleReceivedBy=@userId,
				BarCode=@barCode,
				LastModifiedBy=@userId,
				LastModifiedOn=GETDATE(),
				IpAddress=@ipAddress
			WHERE
				PatientInvestigationId=@patientInvestigationId

		END

		IF(@statusId=6) ---Dispatch
		BEGIN
			UPDATE
				PatientInvestigationDetails
			SET
				 IsDispatched =1,  
				 DispatchedBy=@userId,
				 DispatchedOn=isnull(@sampleDateTime,GETDATE()),
				 LastModifiedBy=@userId,  
				 LastModifiedOn=GETDATE(),  
				 IpAddress=@ipAddress  
			WHERE
				PatientInvestigationId=@patientInvestigationId

		END
END
GO

SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetSampleJourneyByPatientInvestigationId] @PatientInvestigationId INT
AS
BEGIN
	SET NOCOUNT ON;

	SELECT 
		 PID.InvestigationId
		,PID.PatientId
		,sim.name as InvestigationName
		,PID.LabNo
		,PID.BarCode
		,pm.UHID
		,pm.PatientName
		,pm.Age
		,pm.Gender
		,PID.isUrgent
        ,(SELECT TOP 1 STM.SampleType FROM SampleTypeMaster STM WHERE STM.SampleTypeId = isnull(pid.SampleTypeId,sim.SampleTypeId)) AS SampleType
        ,ISNULL(SIM.TATInMin, 0) AS TargetTATInMin

        ,CASE
            WHEN SIM.TATInMin IS NOT NULL
                 AND SIM.TATInMin != 0
                 AND PID.SampleCollectedOn IS NOT NULL
                 AND PID.IsSampleRejected = 0
            THEN DATEDIFF(MINUTE, PID.SampleCollectedOn,
                          ISNULL(PID.ReportApprovedOn, GETDATE()))
            ELSE 0
        END AS ActualTATInMin

		--Registration
		,format(pvd.CreatedOn,'dd-MM-yyyy hh:mm tt') as RegistrationOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = pvd.CreatedBy) AS RegistrationBy

		
		--Sample Rejected
		,PID.isSampleRejected
		,format(PID.SampleRejectedOn,'dd-MM-yyyy hh:mm tt') as SampleRejectedOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.SampleRejectedBy) AS SampleRejectedBy
		,PID.SampleRejectionReason


		--Sample Collection
		,PID.IsSampleCollected
		,format(PID.SampleCollectedOn,'dd-MM-yyyy hh:mm tt') as SampleCollectedOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.SampleCollectedBy) AS SampleCollectedBy
	

		--Department Received
		,PID.IsSampleReceivedByDepartment
		,format(PID.SampleReceivedByDepartmentOn,'dd-MM-yyyy hh:mm tt') as SampleReceivedByDepartmentOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.SampleReceivedBy) AS SampleReceivedBy



		--SentToDepartment
		,PID.IsSentToDepartment
		,format(PID.SentToDepartmentOn,'dd-MM-yyyy hh:mm tt') as SentToDepartmentOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.SentToDepartmentBy) AS SentToDepartmentBy
		


	  --Result Entry
		,PID.IsResultDone
		,format(PID.ResultDoneOn,'dd-MM-yyyy hh:mm tt') as ResultDoneOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.ResultDoneBy) AS ResultDoneBy
		


				--Approved
		,PID.IsReportApproved
		,format(PID.ReportApprovedOn,'dd-MM-yyyy hh:mm tt') as ReportApprovedOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.ReportApprovedBy) AS ReportApprovedBy
		,(SELECT TOP 1 CompleteName FROM DoctorMaster DM WHERE DM.DoctorId = PID.ReportApporvedByDoctorId) AS ReportApporvedByDoctor
	


		--Dispatch
		,PID.IsDispatched
		,format(PID.DispatchedOn,'dd-MM-yyyy hh:mm tt') as DispatchedOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.DispatchedBy) AS DispatchedBy


		--print
		,PID.isReportPrinted
		,format(PID.ReportPrintedOn,'dd-MM-yyyy hh:mm tt') as ReportPrintedOn
		,(SELECT TOP 1 name FROM UserMaster um WHERE um.id = PID.ReportPrintedBy) AS ReportPrintedBy




		
	FROM PatientInvestigationDetails PID
	inner join PatientMaster pm on pm.PatientId=pid.PatientId
	inner join PatientVisitDetails PVD on PVD.VisitId=pid.VisitId
	inner join ServiceItemMaster SIM on SIM.ServiceItemId=pid.InvestigationId
	WHERE PID.PatientInvestigationId = @PatientInvestigationId
END
GO

SET QUOTED_IDENTIFIER ON;
GO


  
CREATE OR ALTER PROCEDURE [dbo].[S_ValidatePatientInvestigationforRefund]    
(    
 @FTDId INT  
)    
 as     
 begin    
  
	select (IsSampleCollected+IsSentToDepartment+IsSampleReceivedByDepartment+IsResultDone+IsReportApproved) Result from PatientInvestigationDetails with (nolock) WHERE FTDId=@FTDId
  
 end
GO

SET QUOTED_IDENTIFIER ON;
GO



CREATE OR ALTER PROCEDURE [dbo].[S_GetPatientInvestigationsForReportPrint] --'1260,1261'  
@PatientInvestigationIdList NVARCHAR(MAX),  
@isHeaderPNG int=0,  
@PrintBy NVARCHAR(20)=null,  
@branchId NVARCHAR(20)=null  
AS  
BEGIN  
 DECLARE @SQL NVARCHAR(MAX);  
  
 SET @SQL=N'SELECT   
   PM.PatientName PName,    
   PM.UHID,    
   PVD.CurrentAge Age,    
   PM.Gender,    
   PM.DOB,    
   PM.Relation,    
   PM.RelativeName Relative,    
   PM.ContactNumber ContactNo,    
   PM.Email,    
   (SELECT TOP 1DM.CompleteName FROM IPDVisitDoctorMapping IDM    
    INNER JOIN DoctorMaster DM ON IDM.DoctorId=DM.DoctorId    
    WHERE VisitId=PVD.VisitId AND IsCurrent=1    
   )ADoctor,  
   (SELECT  DM.CompleteName FROM DoctorMaster DM WHERE DM.DoctorId=PID.DoctorId  
   )InvPrescribedBy,  
   (Select  IsNull(DoctorName,'''') InvReferdBy from ReferDoctorMaster rf where rf.ReferDoctorId= pvd.ReferDoctorId) InvReferdBy,  
  CASE   
  WHEN PVD.TypeId=2 THEN CAST(PVD.VisitNo AS NVARCHAR(20))  
  ELSE   
  '''' END AS IPDNo,    
  --(SELECT CONCAT(WardName,''/'',BedNo) FROM BedMaster WHERE BedId=PVD.BedId)Bed,  
  PID.LabNo,  
  PID.BarCode,  
  FORMAT(PID.CreatedOn,''dd-MMM-yyyy hh:mm: tt'') AS InvBillingDateTime,  
  CASE   
  WHEN PID.IsSampleCollected=1 THEN FORMAT(PID.SampleCollectedOn,''dd-MMM-yyyy hh:mm: tt'')  
  ELSE   
  '''' END AS InvSampleColDateTime,  
  CASE   
  WHEN PID.IsSentToDepartment=1 THEN FORMAT(PID.SentToDepartmentOn,''dd-MMM-yyyy hh:mm: tt'')  
  ELSE   
  '''' END AS InvSentToDepartmentDateTime,  
  CASE   
  WHEN PID.IsSampleReceivedByDepartment=1 THEN FORMAT(PID.SampleReceivedByDepartmentOn,''dd-MMM-yyyy hh:mm: tt'')  
  ELSE   
  '''' END AS InvDeptRecDateTime,    
  CASE   
  WHEN PID.IsReportApproved=1 THEN FORMAT(PID.ReportApprovedOn,''dd-MMM-yyyy hh:mm: tt'')  
  ELSE   
  '''' END AS InvReportApprovalDateTime,   
  FORMAT(PID.ResultDoneOn,''dd-MMM-yyyy hh:mm: tt'') AS InvResultDateTime,  
  (SELECT  DM.CompleteName FROM DoctorMaster DM WHERE DM.DoctorId=PID.ReportApporvedByDoctorId  
   )InvReportApprovedBy,  
  
   (SELECT top 1 DLA.DocSignPath FROM DoctorSignatureMaster DLA WHERE DLA.DoctorId=PID.ReportApporvedByDoctorId and DLA.isDelete=0 and DLA.isActive=1)DoctorSignFilePath,  
   (SELECT top 1 DLA.XSign FROM DoctorSignatureMaster DLA WHERE DLA.DoctorId=PID.ReportApporvedByDoctorId and DLA.isDelete=0 and DLA.isActive=1  )XSign,  
   (SELECT top 1 DLA.YSign FROM DoctorSignatureMaster DLA WHERE DLA.DoctorId=PID.ReportApporvedByDoctorId and DLA.isDelete=0 and DLA.isActive=1  )YSign,  
  
   --(SELECT top 1 RPS.HeaderHeight FROM LabReportPrintSettingMaster RPS) HeaderHeight,  
   --(SELECT top 1 RPS.FooterHeight FROM LabReportPrintSettingMaster RPS) FooterHeight,  
   --(SELECT top 1 RPS.NABLPath FROM LabReportPrintSettingMaster RPS) NABLPath,  
   --(SELECT top 1 RPS.PrintDateTime FROM LabReportPrintSettingMaster RPS) PrintDateTime,  
   --(SELECT top 1 RPS.PreparedBy FROM LabReportPrintSettingMaster RPS) PreparedBy,  
   --(SELECT top 1 RPS.PrintedBy FROM LabReportPrintSettingMaster RPS) PrintedBy,  
   --(SELECT top 1 RPS.PageNumbering FROM LabReportPrintSettingMaster RPS) PageNumbering,  
   --(SELECT top 1 RPS.DoctorSignature FROM LabReportPrintSettingMaster RPS) DoctorSignature,  
   --(SELECT top 1 RPS.TestNameforSingleTest FROM LabReportPrintSettingMaster RPS) TestNameforSingleTest,  
   --(SELECT top 1 RPS.PrintDepartmentSeperate FROM LabReportPrintSettingMaster RPS) PrintDepartmentSeperate,  
   --(SELECT top 1 RPS.SendEmail FROM LabReportPrintSettingMaster RPS) SendEmail,  
   --(SELECT top 1 RPS.SendWhatsAppMessage FROM LabReportPrintSettingMaster RPS) SendWhatsAppMessage,  
   --(SELECT top 1 RPS.XQRCode FROM LabReportPrintSettingMaster RPS) XQRCode,  
   --(SELECT top 1 RPS.YQRCode FROM LabReportPrintSettingMaster RPS) YQRCode,  
   --(SELECT top 1 RPS.QRHeight FROM LabReportPrintSettingMaster RPS) QRHeight,  
   --(SELECT top 1 RPS.QRWidth FROM LabReportPrintSettingMaster RPS) QRWidth,  
   --(SELECT top 1 RPS.QRCodePosition FROM LabReportPrintSettingMaster RPS) QRCodePosition,  
   --(SELECT top 1 RPS.IsActiveQRCode FROM LabReportPrintSettingMaster RPS) IsActiveQRCode,  
   --(SELECT top 1 RPS.XBarcode FROM LabReportPrintSettingMaster RPS) XBarcode,  
   --(SELECT top 1 RPS.YBarcode FROM LabReportPrintSettingMaster RPS) YBarcode,  
   --(SELECT top 1 RPS.BarcodeHeight FROM LabReportPrintSettingMaster RPS) BarcodeHeight,  
   --(SELECT top 1 RPS.BarcodeWidth FROM LabReportPrintSettingMaster RPS) BarcodeWidth,  
   --(SELECT top 1 RPS.BarcodePosition FROM LabReportPrintSettingMaster RPS) BarcodePosition,  
   --(SELECT top 1 RPS.IsActiveBarcode FROM LabReportPrintSettingMaster RPS) IsActiveBarcode,  
   --(SELECT top 1 RPS.RotateBarcode90Degree FROM LabReportPrintSettingMaster RPS) RotateBarcode90Degree,  
   ';  
  
   
    SET @SQL+='  
 (SELECT top 1 LH.LetterHeadFilePath from LabReportLetterHeadMaster LH where LH.BranchId='+@branchId+' and LH.isActive=1) LetterHeadFilePath,  
    (SELECT top 1 LH.PaddingLeft from LabReportLetterHeadMaster LH where LH.BranchId='+@branchId+' and LH.isActive=1) PaddingLeft,  
    (SELECT top 1 LH.PaddingRight from LabReportLetterHeadMaster LH where LH.BranchId='+@branchId+' and LH.isActive=1) PaddingRight,  
    (SELECT top 1 LH.PaddingTop from LabReportLetterHeadMaster LH where LH.BranchId='+@branchId+' and LH.isActive=1) PaddingTop,';  
  
    SET @SQL+='  
   ISNULL(  
    STUFF(  
        (  
            SELECT ''#'' + RFRM.FooterRemarks + ''@'' + CAST(RFRM.InvestigationId AS NVARCHAR)  
            FROM ReportFooterRemarksMaster RFRM       
            WHERE RFRM.IsActive = 1  
            FOR XML PATH(''''), TYPE  
        ).value(''.'', ''NVARCHAR(MAX)''),  
        1, 1, ''''  
    ),  
    ''''  
) AS ReportFooterRemarks,    
  
    (SELECT top 1 NABL.NABLFilePath from NABLMaster NABL where NABL.BranchId='+@branchId+' and NABL.InvestigationId=SIM.ServiceItemId and NABL.isActive=1) NABLFilePath,  
    (SELECT top 1 NABL.IsDefaultLogo from NABLMaster NABL where NABL.BranchId='+@branchId+' and NABL.InvestigationId=SIM.ServiceItemId and NABL.isActive=1) IsDefaultLogo,  
  
  
   (SELECT UM.Name FROM UserMaster UM WHERE UM.ID=PID.ResultDoneBy) InvResultEntryBy,  
   isnull((SELECT UM.Name FROM UserMaster UM WHERE UM.ID='+@PrintBy+'),'''') InvResultPrintBy,  
   PID.PatientInvestigationId,  
   PID.InvestigationId,  
   PID.IsReportApproved,  
   SIM.ReportTypeId,  
   SIM.IsPrintAlone,  
   (SELECT  SSM.SubSubCategoryName FROM SubSubCategoryMaster SSM WHERE SSM.SubSubCategoryId=SIM.SubSubCategoryId) AS Department,  
   (SELECT STM.SampleType FROM SampleTypeMaster STM WHERE STM.SampleTypeId=PID.SampleTypeId) AS SampleType,  
  (SELECT LMM.Method FROM LabMethodMaster LMM WHERE LMM.MethodId=SIM.LabMethodId) AS InvestigationMethod,  
  (SELECT TOP 1 CM.ContentValue FROM InvastigationTemplateCommentMaster CM  
  INNER JOIN InvestigationTemplateInterpretationMapping ITM ON ITM.TypeId=2 AND ITM.InvestigationId=PID.InvestigationId AND ITM.ItemId=CM.Id) AS interpretation  
  ,CASE   
  WHEN PID.IsReportApproved=1 THEN ''Final''  
  ELSE   
  ''Provisional'' END AS InvReportStatus  
  ,pid.InvestigationComment as InvestigationComment,PVD.Type as VisitType  
   ,(select top 1 iom.SubSubCategoryName from SubSubCategoryMaster IOM where iom.subsubCategoryId=sim.subsubCategoryId)SubSubCategoryName  
   
   
  ,isnull((select top 1 iom.OrderId from InvestigationOrderingMaster IOM where iom.typeid=2 and iom.id=sim.ServiceItemId),''2000000'')   as TestWiseOrdering   
  ,isnull((select top 1 iom.OrderId from InvestigationOrderingMaster IOM where iom.typeid=1 and iom.id=sim.subsubCategoryId),''100000'')   as DepartmentWiseOrdering   
      
     
        ,(select top 1 ftd.IsUnderPackage from FinancialTransactionDetails ftd where ftd.FTDId=pid.FTDId) as IsUnderPackage  
        ,case when (select top 1 ftd.IsUnderPackage from FinancialTransactionDetails ftd where ftd.FTDId=pid.FTDId)=1   
   then concat(SIM.Name,'' : '',isnull((select top 1 SIM2.Name from ServiceItemMaster SIM2 where SIM2.ServiceItemId=(select top 1 ftd.PackageId from FinancialTransactionDetails ftd where ftd.FTDId=pid.FTDId)),''''))   
   else SIM.Name end as InvestigationName,  
  ISNULL(    
    STUFF((    
        SELECT ''#'' + ID.UploadFileLocation    
        FROM InvestigationDocument ID    
        WHERE ID.IsActive = 1    
          AND ID.PatientInvestigationId = PID.PatientInvestigationId    
        FOR XML PATH(''''), TYPE    
    ).value(''.'', ''NVARCHAR(MAX)''), 1, 1, '''')    
, '''') AS InvestigationDocumentFile   
  
FROM   
 PatientInvestigationDetails PID   
 INNER JOIN PatientMaster PM ON PID.PatientId=PM.PatientId AND PID.IsResultDone=1  
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId  
 INNER JOIN PatientVisitDetails PVD ON PVD.VisitId=PID.VisitId   
 WHERE PID.PatientInvestigationId IN ('+@PatientInvestigationIdList+')  
 ORDER BY pid.LabNo,DepartmentWiseOrdering,SIM.IsPrintAlone,TestWiseOrdering,SIM.PrintSequence  '      
  
 EXEC sp_executesql @SQL;  
END
GO

SET QUOTED_IDENTIFIER OFF;
GO

/* ---------- 3. SPs (same columns as the real print SPs) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintPreviewInvestigations
    @PatientInvestigationIdList NVARCHAR(MAX) = NULL,   -- '' / '0' = all dummy investigations
    @isHeaderPNG INT = 0,
    @PrintBy     NVARCHAR(20) = NULL,
    @branchId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @All BIT = CASE WHEN ISNULL(LTRIM(RTRIM(@PatientInvestigationIdList)), '') IN ('', '0') THEN 1 ELSE 0 END;

    SELECT
        P.PName, P.UHID, P.Age, P.Gender, P.DOB, P.Relation, P.Relative, P.ContactNo, P.Email,
        P.ADoctor, P.InvPrescribedBy, P.InvReferdBy, P.IPDNo,
        P.LabNo, P.BarCode,
        FORMAT(DATEADD(MINUTE, -150, @Now), 'dd-MMM-yy HH:mm') AS InvBillingDateTime,
        FORMAT(DATEADD(MINUTE, -145, @Now), 'dd-MMM-yy HH:mm') AS InvSampleColDateTime,
        FORMAT(DATEADD(MINUTE, -130, @Now), 'dd-MMM-yy HH:mm') AS InvSentToDepartmentDateTime,
        FORMAT(DATEADD(MINUTE, -120, @Now), 'dd-MMM-yy HH:mm') AS InvDeptRecDateTime,
        FORMAT(DATEADD(MINUTE,  -10, @Now), 'dd-MMM-yy HH:mm') AS InvReportApprovalDateTime,
        FORMAT(DATEADD(MINUTE,  -30, @Now), 'dd-MMM-yy HH:mm') AS InvResultDateTime,
        P.InvReportApprovedBy,
        -- real signature / letter head of the branch so their print settings can be checked
        (SELECT TOP 1 DLA.DocSignPath FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS DoctorSignFilePath,
        (SELECT TOP 1 DLA.XSign       FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS XSign,
        (SELECT TOP 1 DLA.YSign       FROM DoctorSignatureMaster DLA WHERE DLA.isDelete = 0 AND DLA.isActive = 1) AS YSign,
        (SELECT TOP 1 LH.LetterHeadFilePath FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS LetterHeadFilePath,
        (SELECT TOP 1 LH.PaddingLeft  FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingLeft,
        (SELECT TOP 1 LH.PaddingRight FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingRight,
        (SELECT TOP 1 LH.PaddingTop   FROM LabReportLetterHeadMaster LH WHERE LH.BranchId = @branchId AND LH.isActive = 1) AS PaddingTop,
        CAST('' AS NVARCHAR(MAX)) AS ReportFooterRemarks,
        CAST('' AS NVARCHAR(500)) AS NABLFilePath,
        0 AS IsDefaultLogo,
        P.InvResultEntryBy,
        ISNULL((SELECT UM.Name FROM UserMaster UM WHERE CAST(UM.ID AS NVARCHAR(20)) = @PrintBy), '') AS InvResultPrintBy,
        I.DummyInvestigationId AS PatientInvestigationId,
        I.DummyInvestigationId AS InvestigationId,
        1 AS IsReportApproved,
        I.ReportTypeId,
        I.IsPrintAlone,
        I.Department,
        I.SampleType,
        I.InvestigationMethod,
        I.Interpretation AS interpretation,
        'Final' AS InvReportStatus,
        I.InvestigationComment,
        P.VisitType,
        I.Department AS SubSubCategoryName,
        I.SortOrder AS TestWiseOrdering,
        I.SortOrder AS DepartmentWiseOrdering,
        0 AS IsUnderPackage,
        I.InvestigationName,
        CAST('' AS NVARCHAR(MAX)) AS InvestigationDocumentFile
    FROM dbo.LabReportDummyInvestigation I
    INNER JOIN dbo.LabReportDummyPatient P ON P.DummyPatientId = I.DummyPatientId
    WHERE I.IsActive = 1
      AND (@All = 1 OR I.DummyInvestigationId IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@PatientInvestigationIdList, ',')))
    ORDER BY I.SortOrder, I.DummyInvestigationId;
END
GO

SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 3. Drop the old-named procedure ---------- */
DROP PROCEDURE IF EXISTS dbo.S_searchPatientInvestigationForSampleSegregation;
GO

/* ---------- 4. Menu row ---------- */
UPDATE dbo.NavigationSubMenuMaster
   SET SubMenuName    = 'Sent To Department',
       URL            = 'sent-to-department',
       LastModifiedOn = GETDATE()
 WHERE URL = 'sample-segregation' OR SubMenuName = 'Sample Segregation';
GO
