/* ============================================================================
   2026-11-26  Sample Type, Priority and Room Type filters on three lab pages
   Laboratory Help, Result Entry Pathology and Result Entry Radiology get the
   same three search filters as Department Receive. Their list procedures get
   three optional parameters (defaults = no filter, so other callers are not
   affected):
     @sampleTypeId  ISNULL(NULLIF(PID.SampleTypeId,0), SIM.SampleTypeId) = value
                    (PID.SampleTypeId is 0, not NULL, when no sample was picked;
                    same rule as the DefaultSampleTypeId column)
     @roomTypeId    patient's bed is in that room type (BedMaster.TypeId)
     @isUrgent      PID.isUrgent = 0 / 1
   Same conditions as S_searchPatientInvestigationForSampleDepartmentReceiving
   (except the 0 handling above); each value is checked to be a number before\r
   it goes into @SQL.
   Run after 2026-11-26_LabPagesCardColumns.sql. Re-runnable (CREATE OR ALTER).
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE [dbo].[S_SearchPatientInvestigationForLaboratoryHelpDesk]              
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
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@corporateId NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',  
@statusId int=0,
            
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
   PM.ContactNumber,    
   PM.Email,    
   CASE WHEN PVD.TypeId=2 THEN PVD.VisitNo ELSE '''' END AS IPDNo,                      
   PID.LabNo,                      
   '''' AS WardName,                      
   PM.PatientName,                      
   PVD.CurrentAge ,                      
   PM.Gender,                      
   SIM.Name,   
   PID.VisitId,
   PVD.TotalBalanceAmount,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired,                      
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
    ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
           
   PID.IsSampleCollected,               
   CASE WHEN (select top 1 IsSampleCollectionRequired from CompanySettings) = 1 AND SIM.IsSampleRequired = 1 THEN 1 ELSE 0 END AS IsCollectionApplicable,
   CASE WHEN (select top 1 IsSentToDepartmentRequired from CompanySettings) = 1 AND SIM.IsSentToDepartmentRequired = 1 THEN 1 ELSE 0 END AS IsSentToDepartmentApplicable,
   SIM.IsSentToDepartmentRequired,
   PID.IsSentToDepartment,
   PID.isReportPrinted,
   PID.IsAbnormalResult,
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,              
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected
   
  
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0  AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
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
                      
 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId 
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
           
          
 -- Room Type filter (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@roomTypeId AS INT) > 0)
  SET @SQL+=' INNER JOIN BedMaster RTBM ON RTBM.BedId=PVD.BedId AND RTBM.TypeId = '+CAST(TRY_CAST(@roomTypeId AS INT) AS NVARCHAR(20))+' '

 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '              

 -- Sample Type / Priority filters (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@sampleTypeId AS INT) > 0)
  SET @SQL+=' AND ISNULL(NULLIF(PID.SampleTypeId,0),SIM.SampleTypeId) = '+CAST(TRY_CAST(@sampleTypeId AS INT) AS NVARCHAR(20))+' '

 IF(@isUrgent IN ('0','1'))
  SET @SQL+=' AND PID.isUrgent = '+@isUrgent+' '

            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND sim.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''    
  
   IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''              
            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '                      
                      
              
                     
                      
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO
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
@canSampleCollect NVARCHAR(20)='0',
            
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
   CASE WHEN (select top 1 IsSampleCollectionRequired from CompanySettings) = 1 AND SIM.IsSampleRequired = 1 THEN 1 ELSE 0 END AS IsCollectionApplicable,
   CASE WHEN (select top 1 IsSentToDepartmentRequired from CompanySettings) = 1 AND SIM.IsSentToDepartmentRequired = 1 THEN 1 ELSE 0 END AS IsSentToDepartmentApplicable,
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
'
 SET @SQL+='
 
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
  
    
          
 -- Room Type filter (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@roomTypeId AS INT) > 0)
  SET @SQL+=' INNER JOIN BedMaster RTBM ON RTBM.BedId=PVD.BedId AND RTBM.TypeId = '+CAST(TRY_CAST(@roomTypeId AS INT) AS NVARCHAR(20))+' '

 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '              

 -- Sample Type / Priority filters (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@sampleTypeId AS INT) > 0)
  SET @SQL+=' AND ISNULL(NULLIF(PID.SampleTypeId,0),SIM.SampleTypeId) = '+CAST(TRY_CAST(@sampleTypeId AS INT) AS NVARCHAR(20))+' '

 IF(@isUrgent IN ('0','1'))
  SET @SQL+=' AND PID.isUrgent = '+@isUrgent+' '

            
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
GO
CREATE OR ALTER PROCEDURE [dbo].[S_SearchPatientInvestigationForSampleProcessingRadiology]              
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
@investigationId NVARCHAR(20)=NULL,             
@patientName NVARCHAR(20)=NULL,     
@corporateId NVARCHAR(20)=NULL,     
@roleId NVARCHAR(20)='0',  
@statusId int=0,
            
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
   PM.Gender,                      
   SIM.Name,   
   PID.VisitId,
   PVD.TotalBalanceAmount,
   PID.isUrgent, 
   ISNULL(PID.Barcode,'''') AS Barcode,                      
   SIM.IsSampleRequired,                      
   SIM.IsDepartmentReceivingRequired,                      
   PID.PatientInvestigationId ,           
   SIM.ReportTypeId, 
   PID.IsReportHold,
   ISNULL(CONVERT(varchar(100),PID.CreatedOn,100) , '''') CreatedOn,     
    ISNULL(PM.VIPPatient,'''') AS VIPPatient, 
           
   PID.IsSampleCollected,               
   CASE WHEN (select top 1 IsSampleCollectionRequired from CompanySettings) = 1 AND SIM.IsSampleRequired = 1 THEN 1 ELSE 0 END AS IsCollectionApplicable,
   CASE WHEN (select top 1 IsSentToDepartmentRequired from CompanySettings) = 1 AND SIM.IsSentToDepartmentRequired = 1 THEN 1 ELSE 0 END AS IsSentToDepartmentApplicable,
   SIM.IsSentToDepartmentRequired,
   PID.IsSentToDepartment,
   PID.isReportPrinted,
   PID.IsAbnormalResult,
   ISNULL(CONVERT(varchar(100),PID.SampleCollectedOn,100) , '''') SampleCollectedOn,              
   PID.IsSampleReceivedByDepartment,              
   ISNULL(CONVERT(varchar(100),PID.SampleReceivedByDepartmentOn,100) , '''') SampleReceivedByDepartmentOn,              
   PID.IsResultDone,              
   ISNULL(CONVERT(varchar(100),PID.ResultDoneOn,100) , '''') ResultDoneOn,              
   PID.IsReportApproved,              
   ISNULL(CONVERT(varchar(100),PID.ReportApprovedOn,100) , '''') ReportApprovedOn,              
   PID.IsDispatched,              
   ISNULL(CONVERT(varchar(100),PID.DispatchedOn,100) , '''') DispatchedOn  ,
   pid.isSampleRejected
   
  
 FROM                       
 PatientInvestigationDetails PID                       
 INNER JOIN FinancialTransactionDetails FTD ON FTD.FTDId=PID.FTDId AND PID.IsCancel=0  AND FTD.IsCancel=0 AND  FTD.RefundedQTY=0 AND PID.BranchId='+@branchId+' '                     
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
                      
 SET @SQL+=' INNER JOIN PatientMaster PM ON PM.PatientId=PVD.PatientId                      
 INNER JOIN ServiceItemMaster SIM ON SIM.ServiceItemId=PID.InvestigationId   
 INNER JOIN SubCategoryMaster SM ON SM.SubCategoryId=SIM.SubCategoryId  and SM.LabTypeId=2
 --INNER JOIN RoleWiseSubSubCategoryTagging RST ON RST.SubSubCategoryID=SIM.SubSubCategoryID AND RST.IsActive=1 AND RST.BranchId='+@branchId+' AND RST.RoleId='+@roleId+'   
 '            
           
          
 -- Room Type filter (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@roomTypeId AS INT) > 0)
  SET @SQL+=' INNER JOIN BedMaster RTBM ON RTBM.BedId=PVD.BedId AND RTBM.TypeId = '+CAST(TRY_CAST(@roomTypeId AS INT) AS NVARCHAR(20))+' '

 SET @SQL+=' WHERE PID.IsResultDone in (0,1) '              

 -- Sample Type / Priority filters (2026-11-26, same as Department Receiving)
 IF(TRY_CAST(@sampleTypeId AS INT) > 0)
  SET @SQL+=' AND ISNULL(NULLIF(PID.SampleTypeId,0),SIM.SampleTypeId) = '+CAST(TRY_CAST(@sampleTypeId AS INT) AS NVARCHAR(20))+' '

 IF(@isUrgent IN ('0','1'))
  SET @SQL+=' AND PID.isUrgent = '+@isUrgent+' '

            
  IF(@subCategoryId <> '0' AND LEN(@subCategoryId)>0)                      
  SET @SQL+=' AND SM.SubCategoryId ='+@subCategoryId+''            
              
  IF(@subSubCategoryId <>'0' AND LEN(@subSubCategoryId)>0)                      
  SET @SQL+=' AND sim.SubSubCategoryId = '+@subSubCategoryId+''              
              
  IF(@investigationId <>'0' AND LEN(@investigationId)>0)                      
  SET @SQL+=' AND SIM.ServiceItemId = '+@investigationId+''    
  
   IF(@corporateId<>'0' AND LEN(@corporateId)>0)                      
  SET @SQL+=' AND PVD.CorporateId='+@corporateId+''  
            
  IF(@patientName IS NOT NULL AND LEN(@patientName)>0)                      
  SET @SQL+=' AND PM.PatientName like ''%'+@patientName+'%'''              
            
 IF(@fromDate IS NOT NULL AND LEN(@fromDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL)  and (@uhid IS NULL or LEN(@uhid)=0))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)>='''+@fromDate+''' '                      
                      
 IF(@toDate IS NOT NULL AND LEN(@toDate)>0 AND (LEN(@labNo)=0 OR @labNo IS NULL) AND (LEN(@barCode)=0 OR @barCode IS NULL)  and (@uhid IS NULL or LEN(@uhid)=0))                      
  SET @SQL+=' AND CONVERT(date,PID.CreatedOn)<='''+@toDate+''' '                      
                      
              
                     
                      
 SET @SQL+=' ORDER BY PID.LabNo '                  
                   
exec  sp_executesql @SQL;                  
                      
END
GO
