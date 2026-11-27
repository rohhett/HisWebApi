/*  Add "Result Entry Radiology" to the role menu mapping of every role/branch that had the removed
    "Radiology Result Entry" menu (Front Office; Admin already has it) (owner 2026-11-26).
    Run after 2026-11-26_RemoveOldRadiologyResultEntryMenu.sql (reads its backup table).
    Target: GWT_HIS_API. Re-runnable: yes.
    After running: delete Redis keys _RoleWiseMenuMapping_*. */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.RoleWiseMenuMappingMaster_RadiologyOld_Bak_20261126') IS NULL
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

INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn)
SELECT DISTINCT o.HospId, o.BranchId, o.RoleId, @subMenuId, 1, GETDATE()
FROM dbo.RoleWiseMenuMappingMaster_RadiologyOld_Bak_20261126 o
WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster n
                  WHERE n.SubMenuId = @subMenuId AND n.RoleId = o.RoleId
                    AND n.BranchId = o.BranchId AND n.HospId = o.HospId);
PRINT CONCAT('Role menu rows added: ', @@ROWCOUNT);
