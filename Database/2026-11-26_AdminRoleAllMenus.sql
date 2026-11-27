/*  Map every page to the Admin role (owner 2026-11-26), for each branch where the role already has
    menus or users.
    Target: GWT_HIS_API. Re-runnable: yes. Adds role mapping only; no user rights are changed.
    After running: delete Redis keys _RoleWiseMenuMapping_*. */
SET NOCOUNT ON;

DECLARE @roleId int = (SELECT MIN(RoleId) FROM dbo.RoleMaster WHERE LTRIM(RTRIM(RoleName)) = 'Admin');
IF @roleId IS NULL
BEGIN
    PRINT 'Role Admin not found.';
    RETURN;
END

DECLARE @branch TABLE (HospId int, BranchId int);
INSERT @branch
SELECT HospId, BranchId FROM dbo.RoleWiseMenuMappingMaster WHERE RoleId = @roleId
UNION
SELECT HospId, BranchId FROM dbo.UserRoleMapping WHERE RoleId = @roleId AND IsActive = 1 AND TypeId = 1;

INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn)
SELECT b.HospId, b.BranchId, @roleId, s.SubMenuId, 1, GETDATE()
FROM @branch b
CROSS JOIN dbo.NavigationSubMenuMaster s
WHERE s.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster m
                  WHERE m.RoleId = @roleId AND m.BranchId = b.BranchId AND m.HospId = b.HospId
                    AND m.SubMenuId = s.SubMenuId);
PRINT CONCAT('Role menu rows added: ', @@ROWCOUNT);
