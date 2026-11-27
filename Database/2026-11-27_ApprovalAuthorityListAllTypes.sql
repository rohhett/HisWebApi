/* 2026-11-27  Approval Authority list: @approvalTypeId = 0 returns every approval type (the page now
   opens with all rules). Run after 2026-11-27_ApprovalAuthorityBranchDefault.sql. Re-runnable.
   Callers: AdminRepository.GetApprovalAuthorityMasterList only.
   After running, delete the Redis keys _ApprovalAuthorityMaster_TypeId*. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
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
  where @approvalTypeId = 0 or ApprovalTypeId = @approvalTypeId
  order by aam.ApprovalType, aam.BranchId, aam.AmountUpTo;  
 end
GO
