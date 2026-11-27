/*  Give "Result Entry Radiology" to users who had the removed "Radiology Result Entry" menu, for the
    same branch, role and type (owner approved 2026-11-26).
    Run after 2026-11-26_RemoveOldRadiologyResultEntryMenu.sql (reads its backup table).
    Target: GWT_HIS_API. Re-runnable: yes. Role menu mapping is not changed.
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.UserMenuMaster_RadiologyOld_Bak_20261126') IS NULL
BEGIN
    PRINT 'Backup table not found; run 2026-11-26_RemoveOldRadiologyResultEntryMenu.sql first.';
    RETURN;
END

DECLARE @subMenuId int = (SELECT MIN(SubMenuId) FROM dbo.NavigationSubMenuMaster
                          WHERE LTRIM(RTRIM(URL)) = 'result-entry-radiology');
IF @subMenuId IS NULL
BEGIN
    PRINT 'Menu result-entry-radiology not found.';
    RETURN;
END

BEGIN TRANSACTION;

-- an existing but inactive right is switched back on
UPDATE n SET IsActive = 1, LastModifiedBy = 1, LastModifiedOn = GETDATE()
FROM dbo.UserMenuMaster n
WHERE n.SubMenuId = @subMenuId AND n.IsActive = 0
  AND EXISTS (SELECT 1 FROM dbo.UserMenuMaster_RadiologyOld_Bak_20261126 o
              WHERE o.IsActive = 1 AND o.UserId = n.UserId AND o.RoleId = n.RoleId
                AND o.BranchId = n.BranchId AND o.TypeId = n.TypeId);
PRINT CONCAT('Rights re-activated: ', @@ROWCOUNT);

INSERT INTO dbo.UserMenuMaster (TypeId, UserId, RoleId, BranchId, SubMenuId, HospId, IsActive, CreatedBy, CreatedOn)
SELECT DISTINCT o.TypeId, o.UserId, o.RoleId, o.BranchId, @subMenuId, o.HospId, 1, 1, GETDATE()
FROM dbo.UserMenuMaster_RadiologyOld_Bak_20261126 o
WHERE o.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster n
                  WHERE n.SubMenuId = @subMenuId AND n.UserId = o.UserId AND n.RoleId = o.RoleId
                    AND n.BranchId = o.BranchId AND n.TypeId = o.TypeId);
PRINT CONCAT('Rights added: ', @@ROWCOUNT);

COMMIT TRANSACTION;
