/*  Give every user who holds the Pharmacy or Laboratory role the pages mapped to that role, in the
    same branch (owner approved 2026-11-26).
    Run after 2026-11-26_PharmacyRoleMenus.sql and 2026-11-26_LaboratoryRoleMenus.sql.
    Target: GWT_HIS_API. Re-runnable: yes. Only adds (or re-activates) rights; nothing is removed.
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @right TABLE (HospId int, BranchId int, RoleId int, UserId int, SubMenuId int);
INSERT @right
SELECT DISTINCT m.HospId, m.BranchId, m.RoleId, ur.UserId, m.SubMenuId
FROM dbo.RoleMaster r
JOIN dbo.RoleWiseMenuMappingMaster m ON m.RoleId = r.RoleId
JOIN dbo.UserRoleMapping ur ON ur.RoleId = m.RoleId AND ur.BranchId = m.BranchId AND ur.IsActive = 1 AND ur.TypeId = 1
JOIN dbo.NavigationSubMenuMaster s ON s.SubMenuId = m.SubMenuId
WHERE LTRIM(RTRIM(r.RoleName)) IN ('Pharmacy', 'Laboratory');

BEGIN TRANSACTION;

UPDATE u SET IsActive = 1, LastModifiedBy = 1, LastModifiedOn = GETDATE()
FROM dbo.UserMenuMaster u
JOIN @right x ON x.UserId = u.UserId AND x.RoleId = u.RoleId AND x.BranchId = u.BranchId AND x.SubMenuId = u.SubMenuId
WHERE u.TypeId = 1 AND u.IsActive = 0;
PRINT CONCAT('Rights re-activated: ', @@ROWCOUNT);

INSERT INTO dbo.UserMenuMaster (TypeId, UserId, RoleId, BranchId, SubMenuId, HospId, IsActive, CreatedBy, CreatedOn)
SELECT 1, x.UserId, x.RoleId, x.BranchId, x.SubMenuId, x.HospId, 1, 1, GETDATE()
FROM @right x
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster u
                  WHERE u.TypeId = 1 AND u.UserId = x.UserId AND u.RoleId = x.RoleId
                    AND u.BranchId = x.BranchId AND u.SubMenuId = x.SubMenuId);
PRINT CONCAT('Rights added: ', @@ROWCOUNT);

COMMIT TRANSACTION;
