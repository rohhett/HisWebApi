/*  Remove the "Dashboard" menu (owner 2026-11-26): /dashboard is the page every user lands on after
    login (a fixed route in the app), so it needs no menu.
    Target: GWT_HIS_API. Re-runnable: yes.
    Deletes the NavigationSubMenuMaster row with URL 'dashboard' and its role / user / favourite
    mappings. Rows are copied first to the *_Dashboard_Bak_20261126 tables.
    After running: delete Redis keys _UserTabMenu_*, _UserWiseMenuMapping_*, _RoleWiseMenuMapping_*. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ids TABLE (SubMenuId int PRIMARY KEY);
INSERT @ids SELECT SubMenuId FROM dbo.NavigationSubMenuMaster WHERE LTRIM(RTRIM(URL)) = 'dashboard';

IF NOT EXISTS (SELECT 1 FROM @ids)
BEGIN
    PRINT 'No dashboard menu; nothing to do.';
    RETURN;
END


BEGIN TRANSACTION;

IF OBJECT_ID('dbo.NavigationSubMenuMaster_Dashboard_Bak_20261126') IS NULL
    SELECT * INTO dbo.NavigationSubMenuMaster_Dashboard_Bak_20261126
    FROM dbo.NavigationSubMenuMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
IF OBJECT_ID('dbo.UserMenuMaster_Dashboard_Bak_20261126') IS NULL
    SELECT * INTO dbo.UserMenuMaster_Dashboard_Bak_20261126
    FROM dbo.UserMenuMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
IF OBJECT_ID('dbo.RoleWiseMenuMappingMaster_Dashboard_Bak_20261126') IS NULL
    SELECT * INTO dbo.RoleWiseMenuMappingMaster_Dashboard_Bak_20261126
    FROM dbo.RoleWiseMenuMappingMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
IF OBJECT_ID('dbo.RoleWiseUserFavoriteSubMenuMapping_Dashboard_Bak_20261126') IS NULL
    SELECT * INTO dbo.RoleWiseUserFavoriteSubMenuMapping_Dashboard_Bak_20261126
    FROM dbo.RoleWiseUserFavoriteSubMenuMapping WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);

DELETE FROM dbo.RoleWiseUserFavoriteSubMenuMapping WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
PRINT CONCAT('Favourite rows deleted: ', @@ROWCOUNT);
DELETE FROM dbo.UserMenuMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
PRINT CONCAT('User menu rows deleted: ', @@ROWCOUNT);
DELETE FROM dbo.RoleWiseMenuMappingMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
PRINT CONCAT('Role menu rows deleted: ', @@ROWCOUNT);
DELETE FROM dbo.NavigationSubMenuMaster WHERE SubMenuId IN (SELECT SubMenuId FROM @ids);
PRINT CONCAT('Menus deleted: ', @@ROWCOUNT);

COMMIT TRANSACTION;
