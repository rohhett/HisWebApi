/* =====================================================================
   Delete navigation tabs that have no menu under them  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (a second run deletes 0 rows)
   Run after : 2026-11-26_DeleteUnmappedNavigationMenus.sql

   Objects
     1. DATA  dbo.NavigationTabMaster — tabs ABHA, Cancellation, MIS Dashboard, OPD Display,
              Reports, Patient Status, matched by name AND only when no
              NavigationSubMenuMaster row (active or inactive) still points to the tab.
              The role / user mapping tables hold SubMenuId only, so no mapping rows exist
              for a tab. (IPDTabMaster / RoleWiseIPDTabMapping / UserIPDTabMasterMapping are the
              IPD patient tabs, a different list — not touched.)

   After running: clear Redis _UserTabMenu_* (or users log in again).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @names TABLE (TabName nvarchar(200) PRIMARY KEY);
INSERT @names (TabName) VALUES
    ('ABHA'), ('Cancellation'), ('MIS Dashboard'), ('OPD Display'), ('Reports'), ('Patient Status');

SELECT t.TabId, t.TabName
FROM dbo.NavigationTabMaster t
JOIN @names n ON n.TabName = LTRIM(RTRIM(t.TabName))
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster s WHERE s.TabId = t.TabId);

DELETE t
FROM dbo.NavigationTabMaster t
JOIN @names n ON n.TabName = LTRIM(RTRIM(t.TabName))
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster s WHERE s.TabId = t.TabId);
PRINT CONCAT('NavigationTabMaster deleted: ', @@ROWCOUNT);
GO
