/* =============================================================================
   Feature   : Navigation tab icons
   Date      : 2026-11-23
   Target DB : GWT_HIS_API
   Re-runnable: yes
   Objects   :
     1. NavigationTabMaster.FaIconId (data update only, 16 tabs)
   After running: log out and log in again (or clear the menu cache) to see the new icons.
   - Icons are looked up from FaIconMaster by IconClass (not hard-coded Ids),
     so the script works even if FaIconMaster Ids differ between servers.
   - Tabs are matched by TabName (trimmed, case-insensitive).
   - Re-runnable: running it again sets the same values.
   ============================================================================= */
SET NOCOUNT ON;

DECLARE @Map TABLE (TabName NVARCHAR(200), IconClass NVARCHAR(200));
INSERT INTO @Map (TabName, IconClass) VALUES
 (N'Admin',                N'fa-solid fa-user-shield'),
 (N'MIS Dashboard',        N'fa-solid fa-chart-pie'),
 (N'Reports',              N'fa-solid fa-file-lines'),
 (N'OPD Display',          N'fa-solid fa-tv'),
 (N'Billing',              N'fa-solid fa-file-invoice-dollar'),
 (N'Cancellation',         N'fa-solid fa-ban'),
 (N'Rate Master',          N'fa-solid fa-tags'),
 (N'Patient Status',       N'fa-solid fa-hospital-user'),
 (N'EMR',                  N'fa-solid fa-notes-medical'),
 (N'Investigation Master', N'fa-solid fa-microscope'),
 (N'Master Management',    N'fa-solid fa-database'),
 (N'Lab Operation Mgmt.',  N'fa-solid fa-vials'),
 (N'Purchase',             N'fa-solid fa-cart-shopping'),
 (N'ABHA',                 N'fa-solid fa-id-card'),
 (N'IVF',                  N'fa-solid fa-baby-carriage'),
 (N'Lab New',              N'fa-solid fa-flask-vial');

-- Report any icon that is missing from FaIconMaster (those tabs are left unchanged)
SELECT m.TabName, m.IconClass AS MissingIconClass
FROM @Map m
LEFT JOIN FaIconMaster f ON f.IconClass = m.IconClass
WHERE f.Id IS NULL;

UPDATE t
SET    t.FaIconId       = f.Id,
       t.LastModifiedOn = GETDATE()
FROM   NavigationTabMaster t
JOIN   @Map m        ON LTRIM(RTRIM(t.TabName)) = m.TabName
JOIN   FaIconMaster f ON f.IconClass = m.IconClass
WHERE  ISNULL(t.FaIconId, 0) <> f.Id;

PRINT CONCAT(@@ROWCOUNT, ' tab icon(s) updated.');

-- Result
SELECT t.TabId, t.TabName, t.FaIconId, f.IconClass
FROM   NavigationTabMaster t
LEFT JOIN FaIconMaster f ON f.Id = t.FaIconId
ORDER BY t.SequenceNo, t.TabId;
