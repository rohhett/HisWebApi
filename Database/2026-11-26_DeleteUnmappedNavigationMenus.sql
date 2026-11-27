/* =====================================================================
   Delete navigation menus that have no page in the frontend  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (deletes only what still exists; a second run deletes 0 rows)

   Menus are matched by URL, not by SubMenuId, so the script is safe on a server
   whose ids differ from the local database.

   Part A (always): 9 menus with no page built in Frontend\HIS-main\src\screens\routes\index.tsx
     consent-page, milestone-two, corporate-transfer-confirmation, bill-receipt-cancel,
     ipd-bill-cancel, lis-dashboard, opd-patients, collect-report, stock-report
   Part B (only when @DeleteLabDetailPages = 1): sample-journey, sample-result-entry
     These two have no sidebar page, but the lab grids open /sample-journey/:id and
     /sample-result-entry/... and navbar checks hasAccess("sample-journey" / "sample-result-entry")
     against these menu rows. Deleting them sends every user to /no-page from those links.

   Tables (child rows first, no FK constraints exist)
     1. RoleWiseUserFavoriteSubMenuMapping   (user favourites)
     2. UserMenuMaster                       (user-wise menu rights)
     3. RoleWiseMenuMappingMaster            (role-wise menu mapping)
     4. NavigationSubMenuMaster              (menu master)

   After running: clear Redis _UserTabMenu_* (or users log in again) so the sidebars refresh.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @DeleteLabDetailPages bit = 0;   -- set to 1 to also delete sample-journey and sample-result-entry

DECLARE @urls TABLE (URL nvarchar(200) PRIMARY KEY);
INSERT @urls (URL) VALUES
    ('consent-page'), ('milestone-two'), ('corporate-transfer-confirmation'),
    ('bill-receipt-cancel'), ('ipd-bill-cancel'), ('lis-dashboard'),
    ('opd-patients'), ('collect-report'), ('stock-report');
IF @DeleteLabDetailPages = 1
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
