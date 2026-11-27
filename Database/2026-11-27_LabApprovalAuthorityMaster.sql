/* =====================================================================
   Lab Approval Authority Master  -  2026-11-27
   Target DB : GWT_HIS_API (NEW HIS)      Re-runnable: yes
   Run AFTER 2026-11-27_UserMasterProvisionalApproval.sql (uses UserMaster.CanProvisionalApproval).

     1. TABLE  dbo.LabApprovalAuthorityMaster   (BranchId 0 = default)
     2. RIGHTS CanSetupLabApprovalAuthorityFor Pathology / Radiology / Cardiology  (assign in User Authorization)
     3. FUNC   fn_HasLabApprovalSetupRight
     4. PROCS  S_GetLabApprovalAuthorityLookups, S_GetLabApprovalAuthorityList,
               IU_LabApprovalAuthorityMaster      (@Result: >0 id, -1 sub department already in another rule,
                                                   -2 invalid selection, -3 no right, 0 unknown id)
               U_UpdateLabApprovalAuthorityStatus (@Result: >0 id, -3 no right, 0 unknown id)
     5. MENU   'Lab Approval Authority Master' / lab-approval-authority-master under 'Investigation Masters',
               added to the Laboratory and Admin roles and their users

   After running: restart the API; delete Redis keys _UserTabMenu_*, _RoleWiseMenuMapping_*, _UserWiseMenuMapping_*;
   assign the three rights in User Authorization; users log in again.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.LabApprovalAuthorityMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabApprovalAuthorityMaster
    (
        LabApprovalAuthorityId int IDENTITY(1,1) NOT NULL,
        HospId                int NOT NULL,
        BranchId              int NOT NULL CONSTRAINT DF_LabApprovalAuthorityMaster_BranchId DEFAULT (0),
        LabTypeId             int NOT NULL,
        SubSubCategoryIds     nvarchar(1000) NOT NULL,      -- comma separated sub departments
        ApprovalFlowId        int NOT NULL,                 -- 1 Sequential, 2 Parallel
        ApprovalFlow          nvarchar(100) NOT NULL,
        IsAllApprovalRequired int NOT NULL,
        ApprovalLevelId       int NOT NULL,                 -- 1 to 4
        ApprovalLevel         nvarchar(100) NOT NULL,
        Level1DoctorIds       nvarchar(1000) NOT NULL,
        Level2DoctorIds       nvarchar(1000) NULL,
        Level3DoctorIds       nvarchar(1000) NULL,
        Level4DoctorIds       nvarchar(1000) NULL,
        ProvisionalUserIds    nvarchar(1000) NULL,
        IsActive              int NOT NULL CONSTRAINT DF_LabApprovalAuthorityMaster_IsActive DEFAULT (1),
        CreatedBy             int NULL,
        CreatedOn             datetime NULL CONSTRAINT DF_LabApprovalAuthorityMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy        int NULL,
        LastModifiedOn        datetime NULL,
        IpAddress             nvarchar(50) NULL,
        CONSTRAINT PK_LabApprovalAuthorityMaster PRIMARY KEY CLUSTERED (LabApprovalAuthorityId)
    );
END
GO

/* ---------- rights ---------- */
INSERT INTO dbo.UserRightsMaster (HospId, Name, DisplayName, Description, IsActive, CreatedOn)
SELECT 1, r.Name, r.DisplayName, r.Description, 1, GETDATE()
FROM (VALUES
    ('CanSetupLabApprovalAuthorityForPathology', 'Can Setup Lab Approval Authority For Pathology',
     'Add and update Pathology rules in Lab Approval Authority Master.'),
    ('CanSetupLabApprovalAuthorityForRadiology', 'Can Setup Lab Approval Authority For Radiology',
     'Add and update Radiology rules in Lab Approval Authority Master.'),
    ('CanSetupLabApprovalAuthorityForCardiology', 'Can Setup Lab Approval Authority For Cardiology',
     'Add and update Cardiology rules in Lab Approval Authority Master.')
) r (Name, DisplayName, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRightsMaster u WHERE u.Name = r.Name);
GO

/* LabTypeId 1 Pathology, 2 Radiology, 3 Cardiology; same matching as S_GetUSerAccessRights */
CREATE OR ALTER FUNCTION dbo.fn_HasLabApprovalSetupRight (@userId int, @branchId int, @roleId int, @labTypeId int)
RETURNS bit
AS
BEGIN
    RETURN CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.UserRightMapping m
        JOIN dbo.UserRightsMaster r ON r.Id = m.UserRightId AND r.IsActive = 1
        WHERE m.UserId = @userId AND m.TypeId = 1 AND m.BranchId = @branchId
          AND (m.RoleId = @roleId OR m.RoleId = 0) AND m.IsActive = 1
          AND r.Name = CASE @labTypeId WHEN 1 THEN 'CanSetupLabApprovalAuthorityForPathology'
                                       WHEN 2 THEN 'CanSetupLabApprovalAuthorityForRadiology'
                                       WHEN 3 THEN 'CanSetupLabApprovalAuthorityForCardiology' END
    ) THEN 1 ELSE 0 END;
END
GO

/* 4 result sets: allowed lab types, sub departments, approving doctors, provisional approval users */
CREATE OR ALTER PROCEDURE dbo.S_GetLabApprovalAuthorityLookups (@userId int, @sessionBranchId int, @roleId int)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT LabTypeId, MIN(LabType) AS LabTypeName
    FROM dbo.SubCategoryMaster
    WHERE IsActive = 1 AND LabTypeId > 0
      AND dbo.fn_HasLabApprovalSetupRight(@userId, @sessionBranchId, @roleId, LabTypeId) = 1
    GROUP BY LabTypeId
    ORDER BY LabTypeId;

    SELECT ss.SubSubCategoryId, ss.SubSubCategoryName, s.LabTypeId
    FROM dbo.SubSubCategoryMaster ss
    JOIN dbo.SubCategoryMaster s ON s.SubCategoryId = ss.SubCategoryId
    WHERE ss.IsActive = 1 AND s.IsActive = 1 AND s.LabTypeId > 0
    ORDER BY ss.SubSubCategoryName;

    SELECT DoctorId, ISNULL(NULLIF(CompleteName, ''), Name) AS DoctorName
    FROM dbo.DoctorMaster
    WHERE IsActive = 1 AND CanApproveLabReport = 1
    ORDER BY DoctorName;

    SELECT Id AS UserId, Name AS UserName
    FROM dbo.UserMaster
    WHERE IsActive = 1 AND CanProvisionalApproval = 1
    ORDER BY Name;
END
GO

/* only the lab types the user may set up */
CREATE OR ALTER PROCEDURE dbo.S_GetLabApprovalAuthorityList (@userId int, @sessionBranchId int, @roleId int)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        a.LabApprovalAuthorityId, a.BranchId,
        CASE WHEN a.BranchId = 0 THEN 'Default'
             ELSE (SELECT TOP 1 BranchName FROM dbo.BranchMaster b WHERE b.BranchId = a.BranchId) END AS BranchName,
        a.LabTypeId,
        (SELECT TOP 1 LabType FROM dbo.SubCategoryMaster s WHERE s.LabTypeId = a.LabTypeId) AS LabTypeName,
        a.SubSubCategoryIds,
        (SELECT STRING_AGG(CAST(x.SubSubCategoryName AS nvarchar(max)), ', ') FROM dbo.SubSubCategoryMaster x
          WHERE x.SubSubCategoryId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.SubSubCategoryIds, ','))) AS SubDepartmentNames,
        a.ApprovalFlowId, a.ApprovalFlow, a.IsAllApprovalRequired, a.ApprovalLevelId, a.ApprovalLevel,
        a.Level1DoctorIds, a.Level2DoctorIds, a.Level3DoctorIds, a.Level4DoctorIds,
        (SELECT STRING_AGG(CAST(ISNULL(NULLIF(d.CompleteName, ''), d.Name) AS nvarchar(max)), ', ') FROM dbo.DoctorMaster d
          WHERE d.DoctorId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.Level1DoctorIds, ','))) AS Level1DoctorNames,
        (SELECT STRING_AGG(CAST(ISNULL(NULLIF(d.CompleteName, ''), d.Name) AS nvarchar(max)), ', ') FROM dbo.DoctorMaster d
          WHERE d.DoctorId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.Level2DoctorIds, ','))) AS Level2DoctorNames,
        (SELECT STRING_AGG(CAST(ISNULL(NULLIF(d.CompleteName, ''), d.Name) AS nvarchar(max)), ', ') FROM dbo.DoctorMaster d
          WHERE d.DoctorId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.Level3DoctorIds, ','))) AS Level3DoctorNames,
        (SELECT STRING_AGG(CAST(ISNULL(NULLIF(d.CompleteName, ''), d.Name) AS nvarchar(max)), ', ') FROM dbo.DoctorMaster d
          WHERE d.DoctorId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.Level4DoctorIds, ','))) AS Level4DoctorNames,
        a.ProvisionalUserIds,
        (SELECT STRING_AGG(CAST(u.Name AS nvarchar(max)), ', ') FROM dbo.UserMaster u
          WHERE u.Id IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(a.ProvisionalUserIds, ','))) AS ProvisionalUserNames,
        a.IsActive,
        (SELECT TOP 1 Name FROM dbo.UserMaster u WHERE u.Id = a.CreatedBy) AS CreatedBy,
        FORMAT(a.CreatedOn, 'dd-MM-yyyy') AS CreatedOn,
        (SELECT TOP 1 Name FROM dbo.UserMaster u WHERE u.Id = a.LastModifiedBy) AS LastModifiedBy,
        FORMAT(a.LastModifiedOn, 'dd-MM-yyyy') AS LastModifiedOn
    FROM dbo.LabApprovalAuthorityMaster a
    WHERE dbo.fn_HasLabApprovalSetupRight(@userId, @sessionBranchId, @roleId, a.LabTypeId) = 1
    ORDER BY a.LabTypeId, a.BranchId, a.LabApprovalAuthorityId;
END
GO

CREATE OR ALTER PROCEDURE dbo.IU_LabApprovalAuthorityMaster
(
    @hospId int,
    @labApprovalAuthorityId int,
    @branchId int,
    @labTypeId int,
    @subSubCategoryIds nvarchar(1000),
    @approvalFlowId int,
    @approvalFlow nvarchar(100),
    @isAllApprovalRequired int,
    @approvalLevelId int,
    @approvalLevel nvarchar(100),
    @level1DoctorIds nvarchar(1000),
    @level2DoctorIds nvarchar(1000) = NULL,
    @level3DoctorIds nvarchar(1000) = NULL,
    @level4DoctorIds nvarchar(1000) = NULL,
    @provisionalUserIds nvarchar(1000) = NULL,
    @isActive int = 1,
    @userId int,
    @sessionBranchId int,
    @roleId int,
    @IpAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @oldLabTypeId int = (SELECT LabTypeId FROM dbo.LabApprovalAuthorityMaster
                                 WHERE LabApprovalAuthorityId = @labApprovalAuthorityId);
    IF @labApprovalAuthorityId > 0 AND @oldLabTypeId IS NULL BEGIN SET @Result = 0; RETURN; END

    IF dbo.fn_HasLabApprovalSetupRight(@userId, @sessionBranchId, @roleId, @labTypeId) = 0
       OR (@oldLabTypeId IS NOT NULL AND dbo.fn_HasLabApprovalSetupRight(@userId, @sessionBranchId, @roleId, @oldLabTypeId) = 0)
    BEGIN SET @Result = -3; RETURN; END

    -- every sub department of the lab type, every doctor an approver, every provisional user flagged
    IF NULLIF(@subSubCategoryIds, '') IS NULL
       OR EXISTS (SELECT 1 FROM STRING_SPLIT(@subSubCategoryIds, ',') v
                  WHERE NOT EXISTS (SELECT 1 FROM dbo.SubSubCategoryMaster ss
                                    JOIN dbo.SubCategoryMaster s ON s.SubCategoryId = ss.SubCategoryId
                                    WHERE ss.SubSubCategoryId = TRY_CAST(v.value AS int) AND s.LabTypeId = @labTypeId))
       OR EXISTS (SELECT 1 FROM STRING_SPLIT(CONCAT_WS(',', NULLIF(@level1DoctorIds, ''), NULLIF(@level2DoctorIds, ''),
                                                      NULLIF(@level3DoctorIds, ''), NULLIF(@level4DoctorIds, '')), ',') v
                  WHERE v.value <> '' AND NOT EXISTS (SELECT 1 FROM dbo.DoctorMaster d
                                    WHERE d.DoctorId = TRY_CAST(v.value AS int) AND d.CanApproveLabReport = 1 AND d.IsActive = 1))
       OR EXISTS (SELECT 1 FROM STRING_SPLIT(ISNULL(@provisionalUserIds, ''), ',') v
                  WHERE v.value <> '' AND NOT EXISTS (SELECT 1 FROM dbo.UserMaster u
                                    WHERE u.Id = TRY_CAST(v.value AS int) AND u.CanProvisionalApproval = 1 AND u.IsActive = 1))
    BEGIN SET @Result = -2; RETURN; END

    BEGIN TRANSACTION;

    -- one rule per sub department for the same branch and lab type
    IF EXISTS (SELECT 1
               FROM dbo.LabApprovalAuthorityMaster a WITH (UPDLOCK, HOLDLOCK)
               CROSS APPLY STRING_SPLIT(a.SubSubCategoryIds, ',') saved
               JOIN STRING_SPLIT(@subSubCategoryIds, ',') chosen ON chosen.value = saved.value
               WHERE a.HospId = @hospId AND a.BranchId = @branchId AND a.LabTypeId = @labTypeId
                 AND a.LabApprovalAuthorityId <> @labApprovalAuthorityId)
    BEGIN
        COMMIT TRANSACTION;     -- nothing written yet; a rollback would also undo a caller's transaction
        SET @Result = -1;
        RETURN;
    END

    IF @labApprovalAuthorityId = 0
    BEGIN
        INSERT INTO dbo.LabApprovalAuthorityMaster
            (HospId, BranchId, LabTypeId, SubSubCategoryIds, ApprovalFlowId, ApprovalFlow, IsAllApprovalRequired,
             ApprovalLevelId, ApprovalLevel, Level1DoctorIds, Level2DoctorIds, Level3DoctorIds, Level4DoctorIds,
             ProvisionalUserIds, IsActive, CreatedBy, CreatedOn, IpAddress)
        VALUES
            (@hospId, @branchId, @labTypeId, @subSubCategoryIds, @approvalFlowId, @approvalFlow, @isAllApprovalRequired,
             @approvalLevelId, @approvalLevel, @level1DoctorIds, @level2DoctorIds, @level3DoctorIds, @level4DoctorIds,
             @provisionalUserIds, @isActive, @userId, GETDATE(), @IpAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.LabApprovalAuthorityMaster
        SET BranchId = @branchId, LabTypeId = @labTypeId, SubSubCategoryIds = @subSubCategoryIds,
            ApprovalFlowId = @approvalFlowId, ApprovalFlow = @approvalFlow, IsAllApprovalRequired = @isAllApprovalRequired,
            ApprovalLevelId = @approvalLevelId, ApprovalLevel = @approvalLevel,
            Level1DoctorIds = @level1DoctorIds, Level2DoctorIds = @level2DoctorIds,
            Level3DoctorIds = @level3DoctorIds, Level4DoctorIds = @level4DoctorIds,
            ProvisionalUserIds = @provisionalUserIds, IsActive = @isActive,
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
        WHERE LabApprovalAuthorityId = @labApprovalAuthorityId;
        SET @Result = @labApprovalAuthorityId;
    END

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.U_UpdateLabApprovalAuthorityStatus
(
    @labApprovalAuthorityId int,
    @isActive int,
    @userId int,
    @sessionBranchId int,
    @roleId int,
    @IpAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @labTypeId int = (SELECT LabTypeId FROM dbo.LabApprovalAuthorityMaster
                              WHERE LabApprovalAuthorityId = @labApprovalAuthorityId);
    IF @labTypeId IS NULL BEGIN SET @Result = 0; RETURN; END
    IF dbo.fn_HasLabApprovalSetupRight(@userId, @sessionBranchId, @roleId, @labTypeId) = 0 BEGIN SET @Result = -3; RETURN; END

    UPDATE dbo.LabApprovalAuthorityMaster
    SET IsActive = @isActive, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE LabApprovalAuthorityId = @labApprovalAuthorityId;
    SET @Result = @labApprovalAuthorityId;
END
GO

/* ---------- menu ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'lab-approval-authority-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Investigation Masters' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Investigation Masters" not found - add the page from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        SELECT @tabId, 'Lab Approval Authority Master', 1, 1, 'lab-approval-authority-master', GETDATE(), 1, '127.0.0.1',
               ISNULL(MAX(SequenceNo), 0) + 1
        FROM dbo.NavigationSubMenuMaster WHERE TabId = @tabId;
END
GO

/* Laboratory and Admin roles get the page, and every user of those roles (same as 2026-11-26_LabTATMonitor) */
SET XACT_ABORT ON;
DECLARE @subMenuId int = (SELECT TOP 1 SubMenuId FROM dbo.NavigationSubMenuMaster WHERE LTRIM(RTRIM(URL)) = 'lab-approval-authority-master');
IF @subMenuId IS NULL
    PRINT 'Menu row missing - role and user rights not added.';
ELSE
BEGIN
    DECLARE @role TABLE (RoleId int PRIMARY KEY);
    INSERT @role SELECT MIN(RoleId) FROM dbo.RoleMaster WHERE LTRIM(RTRIM(RoleName)) IN ('Laboratory', 'Admin') GROUP BY LTRIM(RTRIM(RoleName));

    DECLARE @branch TABLE (HospId int, BranchId int, RoleId int);
    INSERT @branch
    SELECT HospId, BranchId, RoleId FROM dbo.RoleWiseMenuMappingMaster WHERE RoleId IN (SELECT RoleId FROM @role)
    UNION
    SELECT HospId, BranchId, RoleId FROM dbo.UserRoleMapping WHERE RoleId IN (SELECT RoleId FROM @role) AND IsActive = 1 AND TypeId = 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn)
    SELECT b.HospId, b.BranchId, b.RoleId, @subMenuId, 1, GETDATE()
    FROM @branch b
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster m
                      WHERE m.RoleId = b.RoleId AND m.BranchId = b.BranchId AND m.HospId = b.HospId AND m.SubMenuId = @subMenuId);
    PRINT CONCAT('Role menu rows added: ', @@ROWCOUNT);

    INSERT INTO dbo.UserMenuMaster (TypeId, UserId, RoleId, BranchId, SubMenuId, HospId, IsActive, CreatedBy, CreatedOn)
    SELECT DISTINCT 1, ur.UserId, ur.RoleId, ur.BranchId, @subMenuId, b.HospId, 1, 1, GETDATE()
    FROM dbo.UserRoleMapping ur
    JOIN @branch b ON b.RoleId = ur.RoleId AND b.BranchId = ur.BranchId
    WHERE ur.IsActive = 1 AND ur.TypeId = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster u
                      WHERE u.TypeId = 1 AND u.UserId = ur.UserId AND u.RoleId = ur.RoleId
                        AND u.BranchId = ur.BranchId AND u.SubMenuId = @subMenuId);
    PRINT CONCAT('User menu rows added: ', @@ROWCOUNT);

    COMMIT TRANSACTION;
END
GO
