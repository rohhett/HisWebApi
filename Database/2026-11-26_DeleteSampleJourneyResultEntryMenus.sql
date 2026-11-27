/* =====================================================================
   Delete the "Sample Journey" and "Sample Result Entry" sidebar menus  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (deletes only what still exists; a second run deletes 0 rows)

   Owner: both menu items have no page, delete them from the navigation master.
   Both route keys are commented out in Frontend\HIS-main\src\screens\routes\index.tsx,
   so the sidebar items open /sample-journey and /sample-result-entry, which fall
   through to /no-page.

   The detail pages stay reachable: the lab grids open /sample-journey/:id and
   /sample-result-entry/:department/:uhid/:visitId/:labNo/:pId, which are declared
   directly in screens/navbar/index.tsx. Their hasAccess() check
   (navbar/components/accessControl.ts) always returns true and does not read these
   menu rows. (This corrects the note in 2026-11-26_DeleteUnmappedNavigationMenus.sql
   Part B; that script still leaves @DeleteLabDetailPages = 0.)

   Menus are matched by URL, not by SubMenuId, so the script is safe on a server
   whose ids differ from the local database (locally 10064 and 10065).

   Tables (child rows first, no FK constraints exist)
     1. RoleWiseUserFavoriteSubMenuMapping   (user favourites)
     2. UserMenuMaster                       (user-wise menu rights)
     3. RoleWiseMenuMappingMaster            (role-wise menu mapping)
     4. NavigationSubMenuMaster              (menu master)

   After running: clear Redis _UserTabMenu_* and users log in again (the menu list
   is kept in the browser at login) so the sidebars refresh.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @urls TABLE (URL nvarchar(200) PRIMARY KEY);
INSERT @urls (URL) VALUES ('sample-journey'), ('sample-result-entry');

DECLARE @ids TABLE (SubMenuId int PRIMARY KEY);
INSERT @ids (SubMenuId)
SELECT s.SubMenuId
FROM dbo.NavigationSubMenuMaster s
JOIN @urls u ON u.URL = LOWER(LTRIM(RTRIM(s.URL)));

SELECT s.SubMenuId, s.SubMenuName, s.URL FROM dbo.NavigationSubMenuMaster s JOIN @ids i ON i.SubMenuId = s.SubMenuId;

BEGIN TRY
    BEGIN TRAN;

    DELETE f FROM dbo.RoleWiseUserFavoriteSubMenuMapping f JOIN @ids i ON i.SubMenuId = f.SubMenuId;
    PRINT CONCAT('RoleWiseUserFavoriteSubMenuMapping deleted: ', @@ROWCOUNT);

    DELETE m FROM dbo.UserMenuMaster m JOIN @ids i ON i.SubMenuId = m.SubMenuId;
    PRINT CONCAT('UserMenuMaster deleted: ', @@ROWCOUNT);

    DELETE r FROM dbo.RoleWiseMenuMappingMaster r JOIN @ids i ON i.SubMenuId = r.SubMenuId;
    PRINT CONCAT('RoleWiseMenuMappingMaster deleted: ', @@ROWCOUNT);

    DELETE s FROM dbo.NavigationSubMenuMaster s JOIN @ids i ON i.SubMenuId = s.SubMenuId;
    PRINT CONCAT('NavigationSubMenuMaster deleted: ', @@ROWCOUNT);

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH
GO
