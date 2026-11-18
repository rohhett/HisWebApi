/* =====================================================================
   Medical Store Master - one menu entry for all store lookup masters      2026-11-17
   Database : GWT_HIS_API          Re-runnable (works by URL, not by SubMenuId)

   The frontend now has ONE page 'Medical Store Master' (medical-store-master) with tabs:
     Manufacturer, Salt / Generic, GST Slab, Drug Category, Dosage Form, Purchase Unit, Rack.
   The 5 separate pages were removed from the frontend, so their menu rows are removed here.

     1. MENU ROW  dbo.NavigationSubMenuMaster  + 'Medical Store Master' / medical-store-master (tab 'Purchase')
     2. ACCESS    every role / user / favourite that had ANY of the 5 old pages gets the new page
                  (RoleWiseMenuMappingMaster, UserMenuMaster, RoleWiseUserFavoriteSubMenuMapping)
     3. DELETE    the 5 old menu rows and all their mappings (HARD DELETE, owner decision):
                    manufacturer-master, salt-master, gst-slab-master, drug-category-master, rack-master

   No table / SP / API change: the tabs use the same Store API endpoints as before
   (Dosage Form and Purchase Unit tabs use the existing getDosageFormList / getPurchaseUnitList).

   After running: clear the menu cache in Redis (keys _UserWiseMenuMapping_*, _RoleWiseMenuMapping_*,
   _UserTabMenu_*) or restart Redis, then log in again.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @old TABLE (URL nvarchar(200) PRIMARY KEY);
INSERT INTO @old VALUES ('manufacturer-master'), ('salt-master'), ('gst-slab-master'), ('drug-category-master'), ('rack-master');

DECLARE @oldIds TABLE (SubMenuId int PRIMARY KEY, HospId int);
INSERT INTO @oldIds SELECT sm.SubMenuId, sm.HospId FROM dbo.NavigationSubMenuMaster sm JOIN @old o ON o.URL = sm.URL;

BEGIN TRAN;

/* ---------- 1. New menu row (one per hospital that had any old row; hospital 1 if none) ---------- */
DECLARE @hosp TABLE (HospId int PRIMARY KEY);
INSERT INTO @hosp SELECT DISTINCT HospId FROM @oldIds;
IF NOT EXISTS (SELECT 1 FROM @hosp) INSERT INTO @hosp VALUES (1);

DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                      WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
IF @tabId IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('Tab "Purchase" not found - nothing changed. Add the tab first, or change the tab name in this script.', 16, 1);
    RETURN;
END

INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
SELECT @tabId, 'Medical Store Master', h.HospId, 1, 'medical-store-master', GETDATE(), 1, '127.0.0.1', 80
FROM @hosp h
WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster x WHERE x.URL = 'medical-store-master' AND x.HospId = h.HospId);

DECLARE @new TABLE (SubMenuId int PRIMARY KEY, HospId int);
INSERT INTO @new SELECT SubMenuId, HospId FROM dbo.NavigationSubMenuMaster WHERE URL = 'medical-store-master';

/* ---------- 2. Carry access over to the new page ---------- */
-- roles
INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn, IpAddress)
SELECT DISTINCT r.HospId, r.BranchId, r.RoleId, n.SubMenuId, 1, GETDATE(), '127.0.0.1'
FROM dbo.RoleWiseMenuMappingMaster r
JOIN @oldIds o ON o.SubMenuId = r.SubMenuId
JOIN @new n    ON n.HospId = r.HospId
WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster x
                  WHERE x.HospId = r.HospId AND x.BranchId = r.BranchId AND x.RoleId = r.RoleId AND x.SubMenuId = n.SubMenuId);
PRINT CONCAT('Role mappings added: ', @@ROWCOUNT);

-- users (active rights only)
INSERT INTO dbo.UserMenuMaster (TypeId, UserId, RoleId, BranchId, SubMenuId, HospId, IsActive, CreatedBy, CreatedOn, IpAddress)
SELECT DISTINCT u.TypeId, u.UserId, u.RoleId, u.BranchId, n.SubMenuId, u.HospId, 1, 1, GETDATE(), '127.0.0.1'
FROM dbo.UserMenuMaster u
JOIN @oldIds o ON o.SubMenuId = u.SubMenuId
JOIN @new n    ON n.HospId = u.HospId
WHERE u.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.UserMenuMaster x
                  WHERE x.HospId = u.HospId AND x.BranchId = u.BranchId AND x.UserId = u.UserId AND x.RoleId = u.RoleId
                    AND ISNULL(x.TypeId, 0) = ISNULL(u.TypeId, 0) AND x.SubMenuId = n.SubMenuId);
PRINT CONCAT('User mappings added: ', @@ROWCOUNT);

-- favourites (table has no HospId: the new row of the old row's hospital)
INSERT INTO dbo.RoleWiseUserFavoriteSubMenuMapping (BranchId, UserId, RoleId, SubMenuId, CreatedBy, CreatedOn, IpAddress)
SELECT DISTINCT f.BranchId, f.UserId, f.RoleId, n.SubMenuId, 1, GETDATE(), '127.0.0.1'
FROM dbo.RoleWiseUserFavoriteSubMenuMapping f
JOIN @oldIds o ON o.SubMenuId = f.SubMenuId
JOIN @new n    ON n.HospId = o.HospId
WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleWiseUserFavoriteSubMenuMapping x
                  WHERE ISNULL(x.BranchId, 0) = ISNULL(f.BranchId, 0) AND x.UserId = f.UserId
                    AND ISNULL(x.RoleId, 0) = ISNULL(f.RoleId, 0) AND x.SubMenuId = n.SubMenuId);
PRINT CONCAT('Favourites added: ', @@ROWCOUNT);

/* ---------- 3. Remove the 5 old pages and everything that points at them ---------- */
DELETE f FROM dbo.RoleWiseUserFavoriteSubMenuMapping f JOIN @oldIds o ON o.SubMenuId = f.SubMenuId;
PRINT CONCAT('Old favourites deleted: ', @@ROWCOUNT);
DELETE u FROM dbo.UserMenuMaster u JOIN @oldIds o ON o.SubMenuId = u.SubMenuId;
PRINT CONCAT('Old user mappings deleted: ', @@ROWCOUNT);
DELETE r FROM dbo.RoleWiseMenuMappingMaster r JOIN @oldIds o ON o.SubMenuId = r.SubMenuId;
PRINT CONCAT('Old role mappings deleted: ', @@ROWCOUNT);
DELETE sm FROM dbo.NavigationSubMenuMaster sm JOIN @oldIds o ON o.SubMenuId = sm.SubMenuId;
PRINT CONCAT('Old menu rows deleted: ', @@ROWCOUNT);

COMMIT;

SELECT sm.SubMenuId, sm.HospId, sm.SubMenuName, sm.URL, sm.IsActive,
       (SELECT COUNT(*) FROM dbo.RoleWiseMenuMappingMaster r WHERE r.SubMenuId = sm.SubMenuId) AS Roles,
       (SELECT COUNT(*) FROM dbo.UserMenuMaster u WHERE u.SubMenuId = sm.SubMenuId AND u.IsActive = 1) AS Users
FROM dbo.NavigationSubMenuMaster sm
WHERE sm.URL IN ('medical-store-master', 'manufacturer-master', 'salt-master', 'gst-slab-master', 'drug-category-master', 'rack-master');
