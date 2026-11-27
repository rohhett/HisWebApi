/* =====================================================================================
   2026-11-26  Rename "Lab Sample Management" to "Sample Management"
   Owner: remove "Lab" from Lab Sample Management everywhere.

   NavigationSubMenuMaster: SubMenuName 'Lab Sample Management' -> 'Sample Management',
                            URL 'lab-sample-management'     -> 'sample-management'
   The frontend route key is now "sample-management" (authorizedRouteMap); the old unreachable
   frontend page that used that key was removed (it had no menu row).
   Role / user / favourite mappings use SubMenuId, so they keep working.
   No stored procedure, table or column contains the name.

   Re-runnable: matches the row by its old or new URL. After running, clear the
   _UserTabMenu_* cache (or restart the API) so menus reload.
   ===================================================================================== */
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster
           WHERE URL = 'sample-management' AND SubMenuName <> 'Sample Management')
BEGIN
    RAISERROR('Another menu already uses URL sample-management; rename stopped.', 16, 1);
    RETURN;
END

UPDATE dbo.NavigationSubMenuMaster
SET SubMenuName = 'Sample Management',
    URL = 'sample-management'
WHERE URL IN ('lab-sample-management', 'sample-management')
  AND (SubMenuName <> 'Sample Management' OR URL <> 'sample-management');

PRINT CONCAT('Menu rows renamed: ', @@ROWCOUNT);

SELECT SubMenuId, SubMenuName, URL, IsActive
FROM dbo.NavigationSubMenuMaster
WHERE URL IN ('lab-sample-management', 'sample-management');
GO
