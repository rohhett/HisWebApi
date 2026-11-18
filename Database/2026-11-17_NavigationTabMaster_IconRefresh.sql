/* =====================================================================
   2026-11-17  NavigationTabMaster - refresh tab icons
   ---------------------------------------------------------------------
   Sets a fitting Font Awesome icon (FaIconMaster) on every navigation tab.
   - Icons are matched by IconClass, not by Id, so the script also works
     on a server where FaIconMaster Ids differ.
   - Re-runnable: a row is updated only when its icon actually changes.
   - Prints a warning for any icon class missing from FaIconMaster.
   After running: clear the Redis keys _UserTabMenu_* (the user menu
   cache), or the sidebar keeps the old icons until the cache is cleared.
   ===================================================================== */
SET NOCOUNT ON;

DECLARE @Map TABLE (TabId INT PRIMARY KEY, TabName NVARCHAR(100), IconClass NVARCHAR(500));
INSERT INTO @Map (TabId, TabName, IconClass) VALUES
 (6,    N'Cancellation',         N'fa-solid fa-file-circle-xmark'),
 (1,    N'Admin',                N'fa-solid fa-user-gear'),
 (1003, N'EMR',                  N'fa-solid fa-file-medical'),
 (2003, N'Investigation Master', N'fa-solid fa-flask-vial'),
 (5,    N'Billing',              N'fa-solid fa-receipt'),
 (3004, N'Lab Operation Mgmt.',  N'fa-solid fa-microscope'),
 (3003, N'Master Management',    N'fa-solid fa-database'),
 (4,    N'OPD Display',          N'fa-solid fa-tv'),
 (4004, N'Purchase',             N'fa-solid fa-cart-shopping'),
 (7,    N'Rate Master',          N'fa-solid fa-tags'),
 (8,    N'Patient Status',       N'fa-solid fa-bed-pulse'),
 (3,    N'Reports',              N'fa-solid fa-file-lines'),
 (2,    N'MIS Dashboard',        N'fa-solid fa-gauge-high'),
 (4005, N'ABHA',                 N'fa-solid fa-id-card'),
 (4006, N'IVF',                  N'fa-solid fa-person-pregnant'),
 (4007, N'Lab New',              N'fa-solid fa-vials');

/* Warn about icon classes that do not exist in FaIconMaster */
SELECT m.TabId, m.TabName, m.IconClass AS MissingIconClass
FROM @Map m
WHERE NOT EXISTS (SELECT 1 FROM FaIconMaster f WHERE f.IconClass = m.IconClass AND f.IsActive = 1);

/* Update. Matches the tab by TabId and checks the name too, so a different
   tab that happens to share a TabId on another server is left alone. */
UPDATE t
SET    t.FaIconId       = f.Id,
       t.LastModifiedOn = GETDATE()
FROM   NavigationTabMaster t
JOIN   @Map m        ON m.TabId = t.TabId AND LTRIM(RTRIM(t.TabName)) = m.TabName
CROSS APPLY (SELECT TOP 1 Id FROM FaIconMaster
             WHERE IconClass = m.IconClass AND IsActive = 1
             ORDER BY Id) f
WHERE  ISNULL(t.FaIconId, 0) <> f.Id;

PRINT CONCAT('NavigationTabMaster icons updated: ', @@ROWCOUNT, ' row(s).');

/* Result */
SELECT t.TabId, t.TabName, t.SequenceNo, t.FaIconId, f.IconClass
FROM   NavigationTabMaster t
LEFT JOIN FaIconMaster f ON f.Id = t.FaIconId
ORDER BY t.SequenceNo, t.TabId;
